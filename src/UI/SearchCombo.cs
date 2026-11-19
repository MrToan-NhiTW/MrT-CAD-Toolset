using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using MedGas2024.Core;

namespace MedGas2024.UI
{
    // A drop-down the user can type into: the list narrows to entries containing every
    // typed word (accents and case ignored). Leaving the box always settles on a real
    // entry, so callers can trust Text.
    public sealed class SearchCombo:ComboBox
    {
        string[] all=new string[0];string last="";bool filtering;
        public SearchCombo()
        {
            DropDownStyle=ComboBoxStyle.DropDown;AutoCompleteMode=AutoCompleteMode.None;
            Width=185;DropDownWidth=390;MaxDropDownItems=20;
        }
        public IList<string> Choices{get{return all;}}
        public void SetChoices(IEnumerable<string> names,string selected)
        {
            all=names.Where(n=>n!=null).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            filtering=true;try{Items.Clear();Items.AddRange(all);}finally{filtering=false;}
            Choose(selected??(all.Length>0?all[0]:""));
        }
        public void Choose(string name)
        {
            string match=all.FirstOrDefault(n=>String.Equals(n,name,StringComparison.OrdinalIgnoreCase));
            if(match==null)return;
            filtering=true;try{if(Items.Count!=all.Length){Items.Clear();Items.AddRange(all);}SelectedItem=match;Text=match;last=match;}finally{filtering=false;}
        }
        public string Value{get{Settle();return last;}}
        protected override void OnTextUpdate(EventArgs e)
        {
            base.OnTextUpdate(e);if(filtering)return;
            string query=Text;int caret=SelectionStart;
            var shown=all.Where(n=>RoomSearch.Matches(query,n)).ToArray();
            filtering=true;
            try
            {
                Items.Clear();Items.AddRange(shown.Length>0?shown:all);
                if(shown.Length>0&&Focused&&!DroppedDown){DroppedDown=true;Cursor.Current=Cursors.Default;}
                Text=query;SelectionStart=Math.Min(caret,query.Length);SelectionLength=0;
            }
            finally{filtering=false;}
        }
        protected override void OnSelectedIndexChanged(EventArgs e)
        {
            if(!filtering&&SelectedItem!=null)last=(string)SelectedItem;
            base.OnSelectedIndexChanged(e);
        }
        protected override void OnLeave(EventArgs e){Settle();base.OnLeave(e);}
        protected override void OnDropDownClosed(EventArgs e){base.OnDropDownClosed(e);if(SelectedItem!=null)last=(string)SelectedItem;}
        // Exact entry > single filtered entry > first filtered entry > last valid entry.
        void Settle()
        {
            string text=Text;
            string match=all.FirstOrDefault(n=>String.Equals(n,text,StringComparison.OrdinalIgnoreCase))
                ??all.FirstOrDefault(n=>RoomSearch.Matches(text,n)&&text.Trim().Length>0)
                ??(all.Contains(last)?last:(all.Length>0?all[0]:""));
            if(match!=Text||Items.Count!=all.Length)Choose(match);else last=match;
        }
    }
}
