using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace FSO.LotView.Model
{
    public class RoomLighting
    {
        //TODO: point lights

        // Same room illumination as Blueprint: exterior tint through openings,
        // saved electric contribution, and a minimum readable interior ambient.
        public Color ColorAt(Color outside, bool outdoors)
        {
            if(outdoors)return outside;
            float length=(float)Math.Sqrt(outside.R*outside.R+outside.G*outside.G+outside.B*outside.B);
            var minimum=outside*(length>0?150f/length:0);
            var daylight=outside*(OutsideLight/100f);
            var electric=Color.White*(AmbientLight/100f);
            return new Color(Math.Min(255,Math.Max(minimum.R,daylight.R)+electric.R),
                Math.Min(255,Math.Max(minimum.G,daylight.G)+electric.G),
                Math.Min(255,Math.Max(minimum.B,daylight.B)+electric.B),255);
        }
        public ushort OutsideLight;
        public ushort AmbientLight;
        public short RoomScore;
    }
}
