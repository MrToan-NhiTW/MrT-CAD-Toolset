using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

public static class PackagingTests
{
    [DllImport("msi.dll",CharSet=CharSet.Unicode)]private static extern uint MsiOpenDatabase(string path,IntPtr mode,out uint handle);
    [DllImport("msi.dll",CharSet=CharSet.Unicode)]private static extern uint MsiDatabaseOpenView(uint database,string sql,out uint view);
    [DllImport("msi.dll")]private static extern uint MsiViewExecute(uint view,uint record);
    [DllImport("msi.dll")]private static extern uint MsiViewFetch(uint view,out uint record);
    [DllImport("msi.dll",CharSet=CharSet.Unicode)]private static extern uint MsiRecordGetString(uint record,uint field,StringBuilder value,ref uint length);
    [DllImport("msi.dll")]private static extern int MsiRecordGetInteger(uint record,uint field);
    [DllImport("msi.dll")]private static extern uint MsiRecordReadStream(uint record,uint field,byte[] buffer,ref uint size);
    [DllImport("msi.dll")]private static extern uint MsiCloseHandle(uint handle);
    [DllImport("msi.dll",CharSet=CharSet.Unicode)]private static extern uint MsiVerifyPackage(string path);
    [DllImport("msi.dll",CharSet=CharSet.Unicode)]private static extern uint MsiOpenPackageEx(string path,uint options,out uint handle);
    [DllImport("msi.dll",CharSet=CharSet.Unicode)]private static extern uint MsiDoAction(uint handle,string action);
    [DllImport("msi.dll",CharSet=CharSet.Unicode)]private static extern int MsiEvaluateCondition(uint handle,string expression);
    [DllImport("msi.dll",CharSet=CharSet.Unicode)]private static extern uint MsiGetTargetPath(uint handle,string folder,StringBuilder value,ref uint length);
    [DllImport("msi.dll",CharSet=CharSet.Unicode)]private static extern uint MsiGetProperty(uint handle,string property,StringBuilder value,ref uint length);
    [DllImport("msi.dll",CharSet=CharSet.Unicode)]private static extern int MsiQueryProductState(string product);
    [DllImport("msi.dll")]private static extern uint MsiSetInternalUI(uint level,IntPtr owner);
    [DllImport("msi.dll",CharSet=CharSet.Unicode)]private static extern uint MsiEnableLog(uint mode,string path,uint attributes);
    private static int passed;
    private static readonly List<string> log=new List<string>();
    private static void Require(uint result,string operation){if(result!=0)throw new Exception(operation+" returned "+result);}
    private static void Check(bool ok,string text){if(!ok)throw new Exception(text);passed++;log.Add("PASS: "+text);Console.WriteLine(log.Last());}
    private static string RecordText(uint record,uint column){uint length=32767;var s=new StringBuilder((int)length+1);Require(MsiRecordGetString(record,column,s,ref length),"Read MSI record");return s.ToString();}
    private static List<string[]> Query(uint db,string sql,int count)
    {
        uint view;Require(MsiDatabaseOpenView(db,sql,out view),"Open MSI view");var rows=new List<string[]>();
        try{Require(MsiViewExecute(view,0),"Execute MSI view");uint record;uint status;while((status=MsiViewFetch(view,out record))==0){try{rows.Add(Enumerable.Range(1,count).Select(i=>RecordText(record,(uint)i)).ToArray());}finally{MsiCloseHandle(record);}}if(status!=259)Require(status,"Fetch MSI record");return rows;}
        finally{MsiCloseHandle(view);}
    }
    private static byte[] Stream(uint db,string name)
    {
        uint view;Require(MsiDatabaseOpenView(db,"SELECT `Data` FROM `_Streams` WHERE `Name`='"+name+"'",out view),"Cabinet stream");
        try{Require(MsiViewExecute(view,0),"Read cabinet");uint record;Require(MsiViewFetch(view,out record),"Fetch cabinet");try{using(var output=new MemoryStream()){byte[] buffer=new byte[8192];while(true){uint size=(uint)buffer.Length;Require(MsiRecordReadStream(record,1,buffer,ref size),"Read cabinet bytes");if(size==0)break;output.Write(buffer,0,(int)size);}return output.ToArray();}}finally{MsiCloseHandle(record);}}
        finally{MsiCloseHandle(view);}
    }
    private static string Hash(byte[] value){using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(value));}
    public static int Main(string[] args)
    {
        string root=args[0],msi=Path.Combine(root,"installer","MrT_CAD_Toolset_2.0.2.2_AutoCAD2024_x64.msi"),bundle=Path.Combine(root,"dist","MrT.CAD.Toolset.bundle");
        uint db=0,session=0;uint previous=MsiSetInternalUI(2,IntPtr.Zero);
        try
        {
            Require(MsiEnableLog(0x3fff,Path.Combine(root,"evidence","msi-costing-log.txt"),0),"Enable costing log");
            Require(MsiVerifyPackage(msi),"MsiVerifyPackage");Check(true,"Windows Installer recognizes the generated MSI package");
            Require(MsiOpenDatabase(msi,IntPtr.Zero,out db),"Open MSI database");
            var props=Query(db,"SELECT `Property`,`Value` FROM `Property`",2).ToDictionary(x=>x[0],x=>x[1]);
            Check(props["ProductName"]=="Mr.T CAD tool set"&&props["ProductVersion"]=="2.0.2.2"&&props["ALLUSERS"]=="1","Product identity and per-machine installation are correct");
            Check(props["MSIRESTARTMANAGERCONTROL"]=="Disable"&&props["REBOOT"]=="ReallySuppress","Installer does not automatically close AutoCAD or reboot the computer");
            var manifest=XDocument.Load(Path.Combine(bundle,"PackageContents.xml")).Root;
            Check(manifest.Attribute("Name").Value==props["ProductName"]&&manifest.Attribute("ProductCode").Value==props["ProductCode"]&&manifest.Attribute("UpgradeCode").Value==props["UpgradeCode"],"MSI and AutoCAD bundle identities agree");
            var req=manifest.Element("Components").Element("RuntimeRequirements");Check(req.Attribute("OS").Value=="Win64"&&req.Attribute("Platform").Value=="AutoCAD"&&req.Attribute("SeriesMin").Value=="R24.3"&&req.Attribute("SeriesMax").Value=="R24.3","Auto-load is restricted to full AutoCAD 2024 x64");
            var entry=manifest.Element("Components").Element("ComponentEntry");Check(entry.Attribute("LoadOnAutoCADStartup").Value=="True"&&entry.Attribute("LoadOnAppearance").Value=="False"&&File.Exists(Path.Combine(bundle,entry.Attribute("ModuleName").Value.Replace('/',Path.DirectorySeparatorChar))),"Startup loading targets an existing DLL and avoids hot-loading during install");
            Check(entry.Element("Commands").Elements("Command").Select(c=>c.Attribute("Global").Value).OrderBy(x=>x).SequenceEqual(new[]{"KHIYTE","MRTADDSIZES","MRTALIASES","MRTALIGNR","MRTBLOCKLAY","MRTBLOCKMERGE","MRTBREAK","MRTDENSITY","MRTFIXTEXT","MRTGROUP","MRTHELP","MRTISO","MRTISOADD","MRTISODIAG","MRTJOIN","MRTJOINTOL","MRTLEGEDIT","MRTLEGEND","MRTLEGSYNC","MRTLTYPE","MRTPICKSYSTEM","MRTPIPE","MRTR0","MRTRESTORERAW","MRTRIBBON","MRTROOMINFO","MRTROOMS","MRTSCALE","MRTSEL","MRTSELLAYER","MRTTABLEMASK","MRTTOOLS","MRTUNIFYR","MRTUNISO","MRTVIEW","MRTZ0PL"}),"New toolset command and all compatibility/help commands are registered");
            var z0=manifest.Element("Components").Elements("ComponentEntry").Single(x=>(string)x.Attribute("AppName")=="MrT.Z0Clean");
            Check((string)z0.Attribute("PerDocument")=="True"&&(string)z0.Attribute("LoadOnCommandInvocation")=="True"&&File.Exists(Path.Combine(bundle,((string)z0.Attribute("ModuleName")).Replace('/',Path.DirectorySeparatorChar)))&&z0.Element("Commands").Elements("Command").Select(x=>(string)x.Attribute("Global")).OrderBy(x=>x).SequenceEqual(new[]{"MRTZ0","MRTZ0CLEAN","MRTZ0HELP","MRTZ0SCAN"}),"Z0Clean is installed and its four per-drawing commands are registered");
            Check(Directory.GetFiles(bundle,"*.dll",SearchOption.AllDirectories).Length==4&&!Directory.GetFiles(bundle,"*",SearchOption.AllDirectories).Any(p=>p.EndsWith(".ttf",StringComparison.OrdinalIgnoreCase)||p.EndsWith("AcMgd.dll",StringComparison.OrdinalIgnoreCase)||p.EndsWith("EngineTests.dll",StringComparison.OrdinalIgnoreCase)),"Runtime payload has four DLLs and excludes Autodesk binaries, fonts and test runners");
            Check(File.ReadAllText(Path.Combine(bundle,"Contents","Win64","default-profile.json")).Contains("\"Rows\":[]"),"Packaged library is empty and cannot seed old pipe systems");
            var files=Query(db,"SELECT `File`,`Component_`,`FileName`,`FileSize`,`Version`,`Sequence` FROM `File`",6);
            var components=Query(db,"SELECT `Component`,`Directory_`,`Attributes`,`KeyPath` FROM `Component`",4);
            var directories=Query(db,"SELECT `Directory`,`Directory_Parent`,`DefaultDir` FROM `Directory`",3).ToDictionary(x=>x[0],x=>x);
            Check(files.Count==Directory.GetFiles(bundle,"*",SearchOption.AllDirectories).Length&&components.Count==files.Count&&files.All(f=>components.Any(c=>c[0]==f[1]&&c[2]=="256"&&c[3]==f[0])),"Every payload file has one 64-bit MSI component and a file key path");
            Check(Query(db,"SELECT `Feature_`,`Component_` FROM `FeatureComponents`",2).Count==components.Count,"Every file belongs to the installable feature");
            foreach(var file in files)
            {
                var component=components.Single(c=>c[0]==file[1]);string d=component[1];var segments=new List<string>();var visited=new HashSet<string>();
                while(d!="INSTALLFOLDER"){if(!visited.Add(d)||!directories.ContainsKey(d))throw new Exception("Invalid directory tree");segments.Insert(0,directories[d][2]);d=directories[d][1];}
                string path=Path.Combine(bundle,Path.Combine(segments.Concat(new[]{file[2]}).ToArray()));
                if(!File.Exists(path)||new FileInfo(path).Length!=Int64.Parse(file[3]))throw new Exception("File map mismatch: "+path);
                if(path.EndsWith(".dll")&&System.Diagnostics.FileVersionInfo.GetVersionInfo(path).FileVersion!=file[4])throw new Exception("File version mismatch");
            }
            Check(true,"MSI directory mapping, payload sizes and DLL versions match the built bundle");
            var cabinet=Stream(db,"MrTToolset.cab");Check(Hash(cabinet)==Hash(File.ReadAllBytes(Path.Combine(root,"packaging","msi-work","MrTToolset.cab"))),"Embedded MSI cabinet matches the independently extracted cabinet");
            File.WriteAllBytes(Path.Combine(root,"evidence","embedded-payload.cab"),cabinet);
            var actions=Query(db,"SELECT `Action`,`Type`,`Source`,`Target` FROM `CustomAction`",4);Check(actions.Count==1&&actions[0][1]=="51"&&actions[0][2]=="ARPINSTALLLOCATION","Only custom action sets the ARP path; no scripts, processes or registry-security changes run");
            var sequence=Query(db,"SELECT `Action`,`Sequence` FROM `InstallExecuteSequence`",2).ToDictionary(x=>x[0],x=>Int32.Parse(x[1]));Check(sequence["InstallInitialize"]<sequence["RemoveExistingProducts"]&&sequence["RemoveExistingProducts"]<sequence["InstallFiles"]&&sequence["InstallFiles"]<sequence["InstallFinalize"],"Upgrade removes old components inside the rollback transaction");
            Check(Query(db,"SELECT `UpgradeCode`,`ActionProperty` FROM `Upgrade`",2).Count==2,"Older-version replacement and newer-version detection are declared");
            var controls=Query(db,"SELECT `Dialog_`,`Control`,`Control_Next` FROM `Control`",3);var dialogs=Query(db,"SELECT `Dialog`,`Control_First`,`Control_Default`,`Control_Cancel` FROM `Dialog`",4);
            Check(dialogs.All(d=>d.Skip(1).All(n=>controls.Any(c=>c[0]==d[0]&&c[1]==n)))&&controls.All(c=>System.String.IsNullOrEmpty(c[2])||controls.Any(n=>n[0]==c[0]&&n[1]==c[2])),"Installer dialog focus, default/cancel buttons and tab chains resolve");
            var events=Query(db,"SELECT `Dialog_`,`Control_`,`Event`,`Argument` FROM `ControlEvent`",4);Check(events.All(e=>controls.Any(c=>c[0]==e[0]&&c[1]==e[1]))&&events.Where(e=>e[2]=="NewDialog").All(e=>dialogs.Any(d=>d[0]==e[3])),"Welcome, installation, repair/removal and exit dialog navigation resolves");
            Check(Query(db,"SELECT `File_` FROM `MsiFileHash`",1).Count==files.Count(f=>String.IsNullOrEmpty(f[4])),"All unversioned files carry Windows Installer hashes");
            // Only read/search/cost actions below. Never ExecuteAction, InstallInitialize,
            // InstallFiles, RegisterProduct, or any actual install/remove action.
            Require(MsiOpenPackageEx(msi,1,out session),"Open costing-only session");
            foreach(string action in new[]{"FindRelatedProducts","AppSearch","CostInitialize","FileCost","CostFinalize"})Require(MsiDoAction(session,action),"Dry-run "+action);
            foreach(string originalProduct in new[]{"{57A9D13B-F5B9-4693-96F0-666AE36C86E8}","{824FF74B-8C8B-45CB-AB2A-33A9589820A7}","{CD09EC54-B31D-4D66-B464-3EB2A6D4347E}","{3102CC70-D2F0-4F04-A660-AB1DAA93A216}","{62AE5DD0-EA99-4EF5-B0B5-28BDBDA14CC6}","{C2E7F12B-F591-464E-97AE-2A7AFA4C1C15}","{2694718D-3419-49BB-903A-FB0D08EA04E4}","{07A83F57-E9D1-4024-8470-EC2801B0CC51}","{92D8D939-D505-4489-9B36-60B776405F23}","{0C6572BA-55F9-4936-A34E-BB77BB21141B}","{ABEDBB2A-A473-46F4-9BCC-85EF5DA83AD3}","{4FD9C5F2-BAEE-4519-ADCF-826BBAFC79A3}","{1A90B837-CF2D-41CD-A918-8B3BDB334EC0}","{53D8C745-2AE5-46D2-81B1-ADCFD8F45EF3}","{6A3DD123-51D3-4B26-BEE5-DC28E5A3A68B}","{4DCA9B46-657A-455B-BB1F-B67726D337CE}","{03706074-2397-4B1E-A7C0-BE70AAE8D27B}","{56672746-E654-4BA7-94F9-58F9F2099AAA}","{B7194945-EEE6-4A6C-A064-E7E5DA8CF9CC}","{868A1482-1BB2-4124-8E64-38D96C4D9DFA}","{9388C1C6-13B7-4324-915E-D033033ED518}","{EDE344EA-0D29-4596-9266-E800BAC811D8}","{D41F57C4-D091-4ACD-B4AC-A38B0E05411E}","{AF19DB3A-E547-4B7E-9B40-51FAC67BF024}","{F7A64F32-28E1-41FD-9D59-146EEA3A8106}","{9E835A80-8ADF-48AB-A1CC-79AC52113345}","{5C66D03A-DC2C-4F8F-99AE-64760E3A91F8}","{A9314C83-5C0C-45C1-AE76-34577EE91234}","{D5369161-58FA-48BD-80A2-27EDAD3F6067}","{6547910F-CDC4-4B12-85DA-0C9C234DA785}","{63A0318A-1BEC-434F-BA7E-FBB2E4628B93}","{876678CA-8C15-4F48-BB4C-3B823A73D89D}","{3C08E0B8-8631-4B00-B649-9B483DD195F9}","{28027BF4-6B67-47F0-9141-700B68EF912D}","{5D2CCE4D-6A80-4DC4-90F5-B35BA0DED082}","{9A83678F-A24C-4286-875C-B79217FCCA41}","{A041EFB8-0847-4C7E-9BAE-2CD2081D24F6}","{75A2C87F-8A0C-471D-8A34-360F6211D7CD}","{7849E081-866B-48B0-8C3B-841E82E4E899}","{B226B9AF-3677-43BA-9474-A91165A0C68B}","{D8FCE0F0-672F-45AD-B826-42B83C32D602}","{A86BFA5C-D6A5-419E-857F-70BA79D1A5A0}","{15DB0B8D-4BC5-4E71-B911-4680D1160C5D}","{92315411-D9C5-485C-B757-889019468AFC}","{4B67EBF2-5A98-4E45-B694-9EA0EB5C1D42}"})
                if(MsiQueryProductState(originalProduct)==5){var older=new StringBuilder(32768);uint olderLength=32767;Require(MsiGetProperty(session,"OLDPRODUCTS",older,ref olderLength),"Read upgrade candidates");Check(older.ToString().Contains(originalProduct),"Native upgrade detection finds installed predecessor "+originalProduct+" for replacement");}
            else log.Add("SKIP: predecessor is not registered: "+originalProduct);
            Check(MsiEvaluateCondition(session,"NET48RELEASE >= \"#528040\"")==1,"Native Windows Installer prerequisite search accepts the installed .NET Framework");
            Check(MsiEvaluateCondition(session,"VersionNT64")==1,"Native Windows Installer sees a 64-bit operating system");
            var pathValue=new StringBuilder(32768);uint size=32767;Require(MsiGetTargetPath(session,"INSTALLFOLDER",pathValue,ref size),"Get installation path");
            string expected=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Autodesk","ApplicationPlugins","MrT.CAD.Toolset.bundle");
            Check(String.Equals(pathValue.ToString().TrimEnd('\\'),expected,StringComparison.OrdinalIgnoreCase),"Costed destination is the trusted Program Files ApplicationPlugins folder");
            log.Add("TOTAL PASS: "+passed+". Package/cabinet/schema/costing checks only. No actual install, repair, upgrade or removal performed.");return 0;
        }
        catch(Exception ex){log.Add("FAIL: "+ex);Console.WriteLine(ex);return 1;}
        finally{if(session!=0)MsiCloseHandle(session);if(db!=0)MsiCloseHandle(db);MsiSetInternalUI(previous,IntPtr.Zero);File.WriteAllLines(Path.Combine(root,"evidence","packaging-tests.txt"),log);}
    }
}

