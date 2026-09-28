using System;
using FSO.LotView;
using Microsoft.Xna.Framework;

namespace FreeSims.Tests
{
    internal static class LotProjectionTests
    {
        private static void Near(float actual,float expected)
        {
            if(Math.Abs(actual-expected)>0.0001f) throw new InvalidOperationException("Camera coordinate mismatch: "+actual+" expected "+expected);
        }
        public static bool Run()
        {
            try {
                // These positions lie on the same view ray. Higher geometry must
                // remain nearer even when its projected ground position overlaps.
                var rays=new[]{new Vector3(1,1,(float)Math.Sqrt(2.0/3.0)),
                    new Vector3(1,-1,(float)Math.Sqrt(2.0/3.0)),
                    new Vector3(-1,-1,(float)Math.Sqrt(2.0/3.0)),
                    new Vector3(-1,1,(float)Math.Sqrt(2.0/3.0))};
                for(int r=0;r<4;r++) {
                    for(int zoom=1;zoom<=3;zoom++) {
                        var origin=TS1SpriteLayer.ProjectWithDepth(Vector3.Zero,zoom,r);
                        var front=TS1SpriteLayer.ProjectWithDepth(rays[r],zoom,r);
                        var back=TS1SpriteLayer.ProjectWithDepth(-rays[r],zoom,r);
                        Near(front.X,origin.X); Near(front.Y,origin.Y);
                        Near(front.Z,(float)Math.Sqrt(8.0/3.0)); Near(back.Z,-front.Z);
                        var tile=new Vector3(3,7,2.95f);
                        var point=TS1SpriteLayer.ProjectWithDepth(tile,zoom,r);
                        var far=TS1SpriteLayer.ProjectWithDepth(tile,1,r);
                        Near(point.X,far.X*(1<<(zoom-1))); Near(point.Y,far.Y*(1<<(zoom-1))); Near(point.Z,far.Z);
                        var above=TS1SpriteLayer.ProjectWithDepth(tile+Vector3.UnitZ,zoom,r);
                        Near(above.Z-point.Z,0.5f);
                        if(above.Y>=point.Y) throw new InvalidOperationException("Wall height projects downwards.");
                    }
                }
                Console.WriteLine("PASS LOT CAMERA DEPTH FOUR ROTATIONS THREE ZOOMS");return true;
            } catch(Exception ex) {Console.WriteLine("FAIL LOT CAMERA DEPTH "+ex);return false;}
        }
    }
}
