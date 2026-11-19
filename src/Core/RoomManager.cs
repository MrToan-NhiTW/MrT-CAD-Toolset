using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security;
using System.Text;

namespace MedGas2024.Core
{
    // One placed room block as shown and edited in the room manager.
    public sealed class RoomBlockInfo
    {
        public string Handle{get;set;}public string Space{get;set;}public string Id{get;set;}
        public string Name{get;set;}public string OldName{get;set;}public string NewName{get;set;}public string Status{get;set;}public string Note{get;set;}
        public string Layer{get;set;}public double Height{get;set;}public double Width{get;set;}public int Color{get;set;}
        public string Frame{get;set;}public double Pad{get;set;}public double Radius{get;set;}public bool Mask{get;set;}
        public bool Changed{get;set;}
        public RoomBlockInfo Copy(){return (RoomBlockInfo)MemberwiseClone();}
        public bool SameAs(RoomBlockInfo o)
        {
            return o!=null&&Name==o.Name&&OldName==o.OldName&&NewName==o.NewName&&Status==o.Status&&Note==o.Note&&Layer==o.Layer&&
                Height==o.Height&&Width==o.Width&&Color==o.Color&&Frame==o.Frame&&Pad==o.Pad&&Radius==o.Radius&&Mask==o.Mask;
        }
        // Checks values a user typed; returns null when they can be written.
        public string Problem()
        {
            if(String.IsNullOrWhiteSpace(Name))return "Tên hiển thị đang trống.";
            if(!Profile.Finite(Height)||Height<=0)return "Cao chữ phải lớn hơn 0.";
            if(!Profile.Finite(Width)||Width<0)return "Rộng MTEXT không được âm.";
            if(Color<1||Color>256)return "Màu là chỉ số AutoCAD 1–255 (256 = ByLayer).";
            if(!RoomLabelStyle.Frames.Contains(Frame))return "Khung phải là: "+String.Join(", ",RoomLabelStyle.Frames)+".";
            if(!Profile.Finite(Pad)||Pad<0||Pad>10||!Profile.Finite(Radius)||Radius<0||Radius>10)return "Lề và bo góc là bội số Text height từ 0 đến 10.";
            if(String.IsNullOrWhiteSpace(Layer))return "Layer đang trống.";
            return null;
        }
        public static readonly string[] Columns={"Handle","Không gian","Mã phòng","Tên hiển thị","Tên cũ","Tên mới","Trạng thái","Ghi chú","Layer","Text height","Rộng MTEXT","Màu","Khung","Lề (× Text height)","Bo góc (× Text height)","Nền che"};
        public string[] Cells()
        {
            var c=CultureInfo.InvariantCulture;
            return new[]{Handle,Space,Id,Name,OldName,NewName,Status,Note,Layer,Height.ToString("0.#####",c),Width.ToString("0.#####",c),Color.ToString(c),Frame,Pad.ToString("0.##",c),Radius.ToString("0.##",c),Mask?"Có":"Không"};
        }
    }

    public static class RoomManagerFile
    {
        // Minimal .xlsx with one sheet of text cells (frozen header row, auto filter).
        public static void Write(string path,string sheet,string[] headers,IEnumerable<string[]> rows)
        {
            const string ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            Func<int,string> col=n=>{var s="";while(n>0){n--;s=(char)('A'+n%26)+s;n/=26;}return s;};
            var data=new StringBuilder();int r=0;
            foreach(var line in new[]{headers}.Concat(rows))
            {
                r++;data.Append("<row r=\""+r+"\">");
                for(int i=0;i<line.Length;i++)
                    data.Append("<c r=\""+col(i+1)+r+"\" t=\"inlineStr\""+(r==1?" s=\"1\"":"")+"><is><t xml:space=\"preserve\">"+SecurityElement.Escape(line[i]??"")+"</t></is></c>");
                data.Append("</row>");
            }
            string last=col(headers.Length)+Math.Max(1,r);
            var tmp=path+".tmp";
            if(File.Exists(tmp))File.Delete(tmp);
            using(var file=new FileStream(tmp,FileMode.CreateNew))using(var zip=new ZipArchive(file,ZipArchiveMode.Create))
            {
                Action<string,string> put=(name,xml)=>{using(var w=new StreamWriter(zip.CreateEntry(name).Open(),new UTF8Encoding(false)))w.Write(xml);};
                put("[Content_Types].xml","<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/></Types>");
                put("_rels/.rels","<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                put("xl/workbook.xml","<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\""+ns+"\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\""+SecurityElement.Escape(sheet)+"\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
                put("xl/_rels/workbook.xml.rels","<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
                put("xl/styles.xml","<?xml version=\"1.0\" encoding=\"UTF-8\"?><styleSheet xmlns=\""+ns+"\"><fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills><borders count=\"1\"><border/></borders><cellStyleXfs count=\"1\"><xf/></cellStyleXfs><cellXfs count=\"2\"><xf numFmtId=\"49\"/><xf numFmtId=\"49\" fontId=\"1\" applyFont=\"1\"/></cellXfs></styleSheet>");
                put("xl/worksheets/sheet1.xml","<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\""+ns+"\"><sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews><cols><col min=\"1\" max=\""+headers.Length+"\" width=\"18\" customWidth=\"1\"/></cols><sheetData>"+data+"</sheetData><autoFilter ref=\"A1:"+last+"\"/></worksheet>");
            }
            if(File.Exists(path))File.Delete(path);
            File.Move(tmp,path);
        }

        public static void Export(string path,IEnumerable<RoomBlockInfo> rows)
        {
            Write(path,"Phòng Mr.T",RoomBlockInfo.Columns,rows.Select(r=>r.Cells()));
        }

        // Reads an exported (and edited) sheet back onto the rows, matched by Handle, else by a
        // unique Mã phòng. Returns how many rows changed and lists what could not be matched/read.
        public static int Import(string path,IList<RoomBlockInfo> rows,List<string> problems)
        {
            var sheet=RoomWorkbook.Read(path).Sheets[0];
            var head=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
            for(int c=1;c<=sheet.Columns;c++){var t=sheet.Cell(1,c).Text.Trim();if(t.Length>0&&!head.ContainsKey(t))head[t]=c;}
            if(!head.ContainsKey("Handle")&&!head.ContainsKey("Mã phòng"))throw new InvalidDataException("Không thấy cột Handle hoặc Mã phòng ở dòng 1. Hãy dùng tệp xuất từ công cụ.");
            var byHandle=rows.Where(r=>!String.IsNullOrEmpty(r.Handle)).ToDictionary(r=>r.Handle,StringComparer.OrdinalIgnoreCase);
            var byId=rows.GroupBy(r=>r.Id??"",StringComparer.Ordinal).Where(g=>g.Count()==1).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.Ordinal);
            Func<int,string,string> get=(row,name)=>{int c;return head.TryGetValue(name,out c)?sheet.Cell(row,c).Text:null;};
            var ci=CultureInfo.InvariantCulture;int changed=0;
            foreach(var pair in sheet.Rows.Where(p=>p.Key>1))
            {
                int row=pair.Key;RoomBlockInfo target=null;var h=get(row,"Handle");var id=get(row,"Mã phòng");
                if(!String.IsNullOrWhiteSpace(h))byHandle.TryGetValue(h.Trim(),out target);
                if(target==null&&!String.IsNullOrWhiteSpace(id))byId.TryGetValue(RoomCatalog.Key(id),out target);
                if(target==null){if(!String.IsNullOrWhiteSpace(h)||!String.IsNullOrWhiteSpace(id))problems.Add("Dòng "+row+": không tìm thấy block "+(h??id)+" trong bản vẽ.");continue;}
                var next=target.Copy();
                Action<string,Action<string>> text=(name,set)=>{var v=get(row,name);if(v!=null)set(v.Trim());};
                Action<string,Action<double>> number=(name,set)=>{int c;if(head.TryGetValue(name,out c)&&sheet.Cell(row,c).Number.HasValue){set(sheet.Cell(row,c).Number.Value);return;}var v=get(row,name);double d;if(String.IsNullOrWhiteSpace(v))return;if(Double.TryParse(v.Trim().Replace(',','.'),NumberStyles.Float,ci,out d))set(d);else problems.Add("Dòng "+row+": "+name+" không phải số ("+v+").");};
                text("Tên hiển thị",v=>next.Name=v);text("Tên cũ",v=>next.OldName=v);text("Tên mới",v=>next.NewName=v);text("Trạng thái",v=>next.Status=v);
                text("Ghi chú",v=>next.Note=v);text("Layer",v=>next.Layer=v);text("Khung",v=>next.Frame=v);
                text("Nền che",v=>next.Mask=RoomSearch.Fold(v)=="co"||v=="1"||String.Equals(v,"true",StringComparison.OrdinalIgnoreCase));
                number("Text height",v=>next.Height=v);number("Rộng MTEXT",v=>next.Width=v);number("Màu",v=>next.Color=(int)Math.Round(v));
                number("Lề (× Text height)",v=>next.Pad=v);number("Bo góc (× Text height)",v=>next.Radius=v);
                var problem=next.Problem();
                if(problem!=null){problems.Add("Dòng "+row+" ("+target.Id+"): "+problem);continue;}
                if(next.SameAs(target))continue;
                int index=rows.IndexOf(target);next.Changed=true;rows[index]=next;changed++;
            }
            return changed;
        }
    }
}
