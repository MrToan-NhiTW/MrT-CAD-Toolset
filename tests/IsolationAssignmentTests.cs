using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using MedGas2024.Core;
public static class IsolationAssignmentTests
{
    public static int Main(string[] args)
    {
        var log=new List<string>();try
        {
            var original=new[]{"typed-pipe-A","foreign-unlabelled-pipe","valve-block","room-text"};var family=new[]{"typed-pipe-A","typed-pipe-B","new-size-pipe"};var assigned=new[]{"foreign-unlabelled-pipe","typed-pipe-A"};
            var result=IsolationSelection.AfterAssignment(original,family,assigned);
            if(!result.SequenceEqual(new[]{"typed-pipe-A","foreign-unlabelled-pipe","valve-block","room-text","typed-pipe-B","new-size-pipe"}))throw new Exception("The new pipe family must extend, rather than replace, the previous mixed isolation set.");log.Add("PASS: Keeps typed pipes, foreign unlabelled curves, blocks and text from the old isolation; adds new family without duplicates.");
            var second=IsolationSelection.AfterAssignment(result,new[]{"different-system-C"},new[]{"changed-layer-and-label"});if(!result.All(second.Contains)||!second.Contains("different-system-C")||!second.Contains("changed-layer-and-label"))throw new Exception("Repeated assignment dropped previous review objects.");log.Add("PASS: Repeated assignment to a different system retains the full accumulated working selection.");
            var noFamily=IsolationSelection.AfterAssignment(original,new string[0],new[]{"assigned-without-recognised-label"});if(!original.All(noFamily.Contains)||!noFamily.Contains("assigned-without-recognised-label"))throw new Exception("An unrecognised assigned object vanished.");log.Add("PASS: Assigned objects are retained independently of label/family recognition.");
            var fresh=IsolationSelection.AfterAssignment<string>(null,family,assigned);if(fresh.Distinct().Count()!=fresh.Length||!assigned.All(fresh.Contains))throw new Exception("Fresh isolation lost assigned objects.");log.Add("PASS: A fresh session uses the new family plus actual assigned objects and de-duplicates them.");
            if(original.Length!=4||original[1]!="foreign-unlabelled-pipe")throw new Exception("Snapshot mutated.");log.Add("PASS: Merge does not mutate the pre-assignment snapshot.");
            var graph=new Dictionary<string,string[]>{{"old",new[]{"left","right"}},{"right",new[]{"right1","right2"}}};
            var alive=new HashSet<string>{"left","right1","right2","other"};var roots=new[]{"old","other"};
            if(!IsolationSelection.ResolveSplit(roots,graph,alive.Contains).SequenceEqual(new[]{"left","right1","right2","other"}))throw new Exception("Repeated splitting loses the working set.");
            alive=new HashSet<string>{"old","other"};if(!IsolationSelection.ResolveSplit(roots,graph,alive.Contains).SequenceEqual(new[]{"old","other"}))throw new Exception("Undo cannot recover original working set.");
            alive=new HashSet<string>{"left","right","other"};if(!IsolationSelection.ResolveSplit(roots,graph,alive.Contains).SequenceEqual(new[]{"left","right","other"}))throw new Exception("Partial undo/redo cannot recover split working set.");
            log.Add("PASS: Split, repeated split, undo and partial redo resolve working membership without duplicating objects.");log.Add("RESULT: PASS; selection membership only. Native isolation visibility/restore still needs an AutoCAD GUI test.");return 0;
        }catch(Exception ex){log.Add("RESULT: FAIL\n"+ex);return 1;}finally{foreach(var line in log)Console.WriteLine(line);File.WriteAllLines(Path.Combine(args[0],"evidence","isolation-assignment-tests.txt"),log);}
    }
}
