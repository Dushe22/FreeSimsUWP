using System;
using System.Collections.Generic;
using System.Linq;
using FSO.Common.Platform;
using FSO.Content.TS1;
using FSO.LotView;
using FSO.LotView.Model;
using FSO.SimAntics;
using FSO.SimAntics.Entities;
using FSO.SimAntics.Model;
using FSO.SimAntics.NetPlay.Drivers;
using Microsoft.Xna.Framework;

namespace FreeSims.Tests
{
    public sealed class SpriteScene
    {
        public readonly List<TS1SpriteLayer> Layers = new List<TS1SpriteLayer>();
        public readonly List<Vector2> Positions = new List<Vector2>();
        public Rectangle Bounds;
    }
    public sealed class TS1SpriteFixture : IDisposable
    {
        private readonly bool oldWorld;
        private readonly VMOfflineDriver driver = new VMOfflineDriver();
        private readonly VM vm;
        private readonly VMMultitileGroup chair, sofa;
        public TS1SpriteFixture(GamePaths paths)
        {
            oldWorld = VM.UseWorld; VM.UseWorld = false;
            try {
                vm = new VM(new VMContext(null) { ContentProvider = new TS1ObjectProvider(paths, false) }, null) { TS1 = true };
                vm.VM_SetDriver(driver); vm.Init();
                vm.Context.Architecture = new VMArchitecture(16,16,null,vm.Context);
                driver.Tick(vm);
                chair = vm.Context.CreateObjectInstance(0x26BFBB29, LotTilePos.OUT_OF_WORLD, Direction.NORTH, true);
                sofa = vm.Context.CreateObjectInstance(0x0FBB8BF8, LotTilePos.OUT_OF_WORLD, Direction.NORTH, true);
                if (chair == null || chair.Objects.Count != 1 || sofa == null || sofa.Objects.Count != 3)
                    throw new InvalidOperationException("Missing chair/three-tile sofa.");
                if (chair.ChangePosition(new LotTilePos(80,80,1),Direction.NORTH,vm.Context).Status != VMPlacementError.Success ||
                    sofa.ChangePosition(new LotTilePos(144,144,1),Direction.NORTH,vm.Context).Status != VMPlacementError.Success)
                    throw new InvalidOperationException("Fixture placement failed.");
            } catch { Dispose(); throw; }
        }
        public SpriteScene Read(bool useSofa, int zoom, int rotation)
        {
            var group = useSofa ? sofa : chair;
            var origin = group.Objects[0].Position;
            var scene = new SpriteScene();
            var ordered = group.Objects.Select(e => new {
                Entity=e, Point=TS1SpriteLayer.Project(new Vector3((e.Position.x-origin.x)/16f,(e.Position.y-origin.y)/16f,0),zoom,rotation)
            }).OrderBy(e => e.Point.Y).ThenBy(e => e.Point.X);
            foreach (var item in ordered) foreach (var layer in TS1SpriteLayer.Read(item.Entity,zoom,rotation)) {
                var pos = item.Point + layer.Offset;
                var rect = new Rectangle((int)pos.X,(int)pos.Y,layer.Width,layer.Height);
                scene.Bounds = scene.Layers.Count == 0 ? rect : Rectangle.Union(scene.Bounds,rect);
                scene.Layers.Add(layer); scene.Positions.Add(pos);
            }
            return scene;
        }
        public void Dispose() { driver.CloseNet(); VM.UseWorld = oldWorld; }
    }
    public static class TS1SpriteTests
    {
        public const int Count = 4;
        private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        public static List<string> Run(GamePaths paths, Action<string> log)
        {
            var results = new List<string>();
            Action<string,Action> check = (name,action) => {
                try { action(); results.Add("PASS " + name); } catch(Exception ex) { results.Add("FAIL " + name); log(ex.ToString()); }
                log(results.Last());
            };
            using (var fixture = new TS1SpriteFixture(paths)) {
                check("CHAIR TWELVE SPRITE VIEWS", () => Validate(fixture,false,log));
                check("SOFA THREE TILES TWELVE VIEWS", () => Validate(fixture,true,log));
                check("MIRRORED VIEWS AND OWNED PIXELS", () => {
                    var left=fixture.Read(false,3,1).Layers.Single(); var right=fixture.Read(false,3,2).Layers.Single();
                    Require(!left.Flip && right.Flip && left.Pixels.SequenceEqual(right.Pixels),"Mirrored chair source mismatch.");
                    var original=left.Pixels[0]; left.Pixels[0]=Color.Red;
                    Require(fixture.Read(false,3,1).Layers[0].Pixels[0]==original,"Modified shared IFF data.");
                });
                check("ALPHA AND CAMERA PROJECTION", () => {
                    Require(TS1SpriteLayer.Premultiply(new Color(200,100,50,128))==new Color(100,50,25,128),"Alpha conversion incorrect.");
                    Require(TS1SpriteLayer.Premultiply(new Color(255,0,255,0))==Color.Transparent,"Transparent RGB remains.");
                    var expected=new[]{new Vector2(64,32),new Vector2(-64,32),new Vector2(-64,-32),new Vector2(64,-32)};
                    for(int r=0;r<4;r++) Require(TS1SpriteLayer.Project(Vector3.UnitX,3,r)==expected[r],"Camera projection incorrect.");
                    bool rejected=false; try { fixture.Read(false,0,0); } catch(ArgumentOutOfRangeException) { rejected=true; }
                    Require(rejected,"Invalid zoom accepted.");
                });
            }
            return results;
        }
        private static void Validate(TS1SpriteFixture fixture,bool sofa,Action<string> log)
        {
            for(int z=1;z<=3;z++) for(int r=0;r<4;r++) {
                var scene=fixture.Read(sofa,z,r);
                Require(scene.Layers.Count==(sofa?3:1),"Unexpected visible layer count.");
                Require(scene.Bounds.Width>0 && scene.Bounds.Height>0 && scene.Bounds.Width<500 && scene.Bounds.Height<500,"Unexpected bounds.");
                foreach(var layer in scene.Layers) {
                    Require(layer.Pixels.Any(c=>c.A==0) && layer.Pixels.Any(c=>c.A==255) && layer.Pixels.Any(c=>c.A>0 && c.A<255),"Alpha coverage missing.");
                    Require(layer.Pixels.All(c=>c.R<=c.A && c.G<=c.A && c.B<=c.A),"Not premultiplied.");
                    Require(layer.Depth.Any(d=>d<255),"Depth coverage missing.");
                }
                log("SPRITE " +(sofa?"SOFA":"CHAIR")+" zoom="+z+" rotation="+r+" layers="+scene.Layers.Count+" bounds="+scene.Bounds);
            }
        }
    }
}
