using System;
using System.IO;
using FSO.Common.Platform;
using FSO.LotView;
using FSO.SimAntics;

// CPU-only import audit; errors return a failing exit code with full context.
class Program
{
    static int Main(string[] args)
    {
        try
        {
            if(args.Length<1||args.Length>2)throw new ArgumentException("Usage: SimInspect <game-root> [house-number]");
            int house=args.Length==2?int.Parse(args[1]):5;
            string root=Path.GetFullPath(args[0]);
            if(!Directory.Exists(root))throw new DirectoryNotFoundException("Game root: "+root);
            VM.UseWorld=false;
            string output=Path.GetFullPath("artifacts/sim-inspect");
            var paths=new GamePaths(Path.Combine(output,"Content"),root,Path.Combine(output,"UserData"));
            using(var lot=new TS1LotRenderData(paths,house))
            {
                Console.WriteLine("SIM COUNT "+lot.SimCount);
                foreach(var file in lot.SimSourceFiles)Console.WriteLine("SIM SOURCE "+file);
                for(int level=1;level<=3;level++)for(int rotation=0;rotation<4;rotation++) {
                var view=lot.Build(3,rotation,level);
                if(view.Unsupported!=0 || (level==3 && view.RoofTriangles==0))throw new InvalidDataException("Incomplete saved view: "+string.Join(";",view.Issues));
                foreach(var issue in view.Issues)Console.WriteLine("ISSUE "+issue);
                foreach(var part in view.SimMaterials)Console.WriteLine("PART "+part.Material.Name+" vertices="+part.Vertices.Count);
                Console.WriteLine("PASS SIM INSPECT house="+house+" level="+level+" rotation="+rotation+" sims="+view.SimsRendered+" parts="+view.SimMaterials.Count+" unsupported="+view.Unsupported+" roof="+view.RoofTriangles);
                }
                foreach(var file in lot.MaterialSourceFiles)Console.WriteLine("LOT SOURCE "+file);
            }
            return 0;
        }
        catch(Exception error)
        {
            Console.Error.WriteLine("SIM INSPECT FAILED: "+error);
            return 1;
        }
    }
}
