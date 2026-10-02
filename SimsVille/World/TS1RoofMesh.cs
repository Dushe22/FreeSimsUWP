using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace FSO.LotView
{
    // CPU form of RoofComponent's half-tile rectangle expansion. Overlapping
    // four-sided hips share one depth buffer, giving straight ridges/valleys.
    public static class TS1RoofMesh
    {
        public static List<Vector3[]> Build(bool[] roofable, int width, int height, float elevation, float pitch)
        {
            if (roofable == null || width < 1 || height < 1 || width > 128 || height > 128 || roofable.Length != width * height)
                throw new ArgumentException("Invalid roof footprint.");
            if (float.IsNaN(elevation) || float.IsInfinity(elevation) || float.IsNaN(pitch) || pitch <= 0 || pitch > 2)
                throw new ArgumentOutOfRangeException("pitch");
            var visited = new bool[roofable.Length]; var result = new List<Vector3[]>();
            Func<int,int,bool> allowed = (x,y) => x >= 0 && y >= 0 && x < width && y < height && roofable[y*width+x];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) {
                if (visited[y*width+x] || !allowed(x,y)) continue;
                int left=x, right=x+1, top=y, bottom=y+1;
                // Same expansion order as RoofComponent: +X, -X, +Y, -Y.
                for (int direction=0; direction<4; direction++) {
                    bool expand=true;
                    while (expand) {
                        if (direction<2) {
                            int edge=direction==0?right:left-1;
                            for(int i=top;i<bottom;i++)if(!allowed(edge,i)){expand=false;break;}
                            if(expand){if(direction==0)right++;else left--;}
                        } else {
                            int edge=direction==2?bottom:top-1;
                            for(int i=left;i<right;i++)if(!allowed(i,edge)){expand=false;break;}
                            if(expand){if(direction==2)bottom++;else top--;}
                        }
                    }
                }
                for(int yy=top;yy<bottom;yy++)for(int xx=left;xx<right;xx++)visited[yy*width+xx]=true;
                float l=left/2f,r=right/2f,t=top/2f,b=bottom/2f;
                float inset=Math.Min(r-l,b-t)/2, peak=elevation+inset*pitch;
                var a=new Vector3(l,t,elevation);var c=new Vector3(r,t,elevation);
                var d=new Vector3(r,b,elevation);var e=new Vector3(l,b,elevation);
                var ma=new Vector3(l+inset,t+inset,peak);var mc=new Vector3(r-inset,t+inset,peak);
                var md=new Vector3(r-inset,b-inset,peak);var me=new Vector3(l+inset,b-inset,peak);
                Face(result,a,c,mc,ma);Face(result,c,d,md,mc);
                Face(result,d,e,me,md);Face(result,e,a,ma,me);
            }
            return result;
        }
        private static void Face(List<Vector3[]> result,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            if(Vector3.Cross(b-a,c-a).LengthSquared()>0.000001f)result.Add(new[]{a,b,c});
            if(Vector3.Cross(c-a,d-a).LengthSquared()>0.000001f)result.Add(new[]{a,c,d});
        }
    }
}
