using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MedGas2024.Core;

namespace MedGas2024.UI
{
    // Text box with a filtered pick list. Typing narrows the list to entries that
    // contain every typed word (accents and case ignored); the user then clicks an
    // entry, or moves with Up/Down and presses Enter. Clicking the list never takes
    // focus from the text box, so the clicked entry is the one kept. With AllowNew a
    // name that is not in the list is kept as typed (e.g. a layer to create).
    public sealed class SearchCombo:UserControl
    {
        sealed class PickList:ListBox
        {
            public event Action<int> Picked;
            public PickList(){SetStyle(ControlStyles.Selectable,false);IntegralHeight=false;BorderStyle=BorderStyle.FixedSingle;TabStop=false;}
            protected override void WndProc(ref Message m)
            {
                const int MouseActivate=0x21,LeftDown=0x201,LeftDouble=0x203;
                if(m.Msg==MouseActivate){m.Result=(IntPtr)3;return;} // MA_NOACTIVATE
                if(m.Msg==LeftDown||m.Msg==LeftDouble)
                {
                    int lp=m.LParam.ToInt32();int i=IndexFromPoint(new Point((short)(lp&0xFFFF),(short)((lp>>16)&0xFFFF)));
                    if(i>=0&&Picked!=null)Picked(i);
                    return;
                }
                base.WndProc(ref m);
            }
            protected override void OnMouseMove(MouseEventArgs e){int i=IndexFromPoint(e.Location);if(i>=0&&i!=SelectedIndex)SelectedIndex=i;base.OnMouseMove(e);}
        }
        readonly TextBox box=new TextBox{Dock=DockStyle.Fill};
        readonly Button arrow=new Button{Text="▾",Dock=DockStyle.Right,Width=24,TabStop=false,FlatStyle=FlatStyle.Flat};
        readonly PickList list=new PickList();
        string[] all=new string[0];string last="";bool updating;
        public bool AllowNew{get;set;}
        public event EventHandler ValueChanged;
        public SearchCombo()
        {
            Width=185;Height=box.PreferredHeight;Padding=new Padding(0);Margin=new Padding(3);
            arrow.FlatAppearance.BorderSize=0;
            Controls.Add(box);Controls.Add(arrow);
            box.TextChanged+=delegate{if(!updating&&box.Focused)Show(box.Text);};
            box.KeyDown+=OnKey;
            arrow.Click+=delegate{if(list.Visible)HideList();else{box.Focus();Show("");}};
            list.Picked+=i=>Pick((string)list.Items[i]);
        }
        public IList<string> Choices{get{return all;}}
        public override string Text{get{return box.Text;}set{updating=true;try{box.Text=value??"";}finally{updating=false;}}}
        public void SetChoices(IEnumerable<string> names,string selected)
        {
            all=names.Where(n=>!String.IsNullOrEmpty(n)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            string match=all.FirstOrDefault(n=>String.Equals(n,selected,StringComparison.OrdinalIgnoreCase));
            last=match??(AllowNew&&!String.IsNullOrWhiteSpace(selected)?selected.Trim():(all.Length>0?all[0]:""));
            Text=last;
        }
        public void Choose(string name)
        {
            string match=all.FirstOrDefault(n=>String.Equals(n,name,StringComparison.OrdinalIgnoreCase));
            if(match==null&&!(AllowNew&&!String.IsNullOrWhiteSpace(name)))return;
            Commit(match??name.Trim(),false);
        }
        public string Value{get{Settle();return last;}}
        void Commit(string value,bool notify)
        {
            bool changed=!String.Equals(value,last,StringComparison.Ordinal);
            last=value;Text=value;HideList();
            if((changed||notify)&&ValueChanged!=null)ValueChanged(this,EventArgs.Empty);
        }
        void Pick(string value){Commit(value,true);box.Focus();box.SelectAll();}
        void OnKey(object sender,KeyEventArgs e)
        {
            if(e.KeyCode==Keys.Down||e.KeyCode==Keys.Up)
            {
                if(!list.Visible)Show(box.Text);
                if(list.Items.Count>0){int i=list.SelectedIndex+(e.KeyCode==Keys.Down?1:-1);list.SelectedIndex=Math.Max(0,Math.Min(list.Items.Count-1,i));}
                e.Handled=e.SuppressKeyPress=true;
            }
            else if(e.KeyCode==Keys.Enter&&list.Visible&&list.SelectedIndex>=0){Pick((string)list.SelectedItem);e.Handled=e.SuppressKeyPress=true;}
            else if(e.KeyCode==Keys.Escape&&list.Visible){HideList();Text=last;e.Handled=e.SuppressKeyPress=true;}
        }
        void Show(string query)
        {
            var form=FindForm();if(form==null)return;
            var shown=all.Where(n=>RoomSearch.Matches(query,n)).ToArray();
            list.BeginUpdate();list.Items.Clear();list.Items.AddRange(shown);list.EndUpdate();
            if(shown.Length==0){HideList();return;}
            list.SelectedIndex=Math.Max(0,Array.FindIndex(shown,n=>String.Equals(n,query,StringComparison.OrdinalIgnoreCase)));
            if(list.Parent!=form){if(list.Parent!=null)list.Parent.Controls.Remove(list);form.Controls.Add(list);}
            list.Font=box.Font;
            int rows=Math.Min(shown.Length,12);int height=rows*list.ItemHeight+4;
            var below=form.PointToClient(PointToScreen(new Point(0,Height)));
            var top=below.Y+height>form.ClientSize.Height?form.PointToClient(PointToScreen(Point.Empty)).Y-height:below.Y;
            list.SetBounds(below.X,Math.Max(0,top),Math.Max(Width,Math.Min(420,form.ClientSize.Width-below.X)),height);
            list.Visible=true;list.BringToFront();
        }
        void HideList(){if(list.Visible)list.Visible=false;}
        protected override void OnLeave(EventArgs e){Settle();base.OnLeave(e);}
        // Exact entry > the only entry containing the typed words > a new name (AllowNew) > the last valid entry.
        void Settle()
        {
            string text=box.Text.Trim();
            string exact=all.FirstOrDefault(n=>String.Equals(n,text,StringComparison.OrdinalIgnoreCase));
            if(exact!=null){Commit(exact,false);return;}
            if(AllowNew&&text.Length>0){Commit(text,false);return;}
            var matches=text.Length==0?new string[0]:all.Where(n=>RoomSearch.Matches(text,n)).Take(2).ToArray();
            Commit(matches.Length==1?matches[0]:(all.Contains(last)?last:(all.Length>0?all[0]:"")),false);
        }
        protected override void Dispose(bool disposing){if(disposing&&list.Parent==null)list.Dispose();base.Dispose(disposing);}
    }
}
