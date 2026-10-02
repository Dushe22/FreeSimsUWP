using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using FSO.Common.Platform;
using FSO.Content.TS1;
using FSO.Files.FAR1;
using FSO.Files.Formats.IFF.Chunks;
using FSO.Vitaboy;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FSO.LotView
{
    // Read-only visual people. No global Content, VM threads, routing or save writes.
    // CPU resources belong to this lot; views contain only baked vertices/pixels/scalars.
    public sealed class TS1SimVisuals : IDisposable
    {
        public sealed class Part {public TS1MaterialProvider.Material Texture;public VertexPositionNormalTexture[] Vertices;}
        public sealed class Person {
            public short ID;public uint GUID;public string Name,Kind;public Vector3 Position;public int Direction;
            public OBJM.PersonVisual Appearance;public readonly List<Part> Parts=new List<Part>();
        }
        private sealed class Source {
            public string File,Entry;
            public int Length;
            public byte[] Read() {
                if(Length<1||Length>16*1024*1024)throw new InvalidDataException("Avatar resource exceeds byte budget.");
                if(Entry==null){var bytes=System.IO.File.ReadAllBytes(File);if(bytes.Length!=Length)throw new IOException("Avatar source changed during load.");return bytes;}
                var archive=new FAR1Archive(File,false);
                try {var bytes=archive.GetEntry(new KeyValuePair<string,byte[]>(Entry,null));if(bytes==null||bytes.Length!=Length)throw new InvalidDataException("Truncated avatar resource.");return bytes;}finally{archive.Close();}
            }
        }
        private readonly Dictionary<string,Source> sources=new Dictionary<string,Source>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string,Source> appearances=new Dictionary<string,Source>(StringComparer.OrdinalIgnoreCase),skeletons=new Dictionary<string,Source>(StringComparer.OrdinalIgnoreCase),animations=new Dictionary<string,Source>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Source,BCF> bcfs=new Dictionary<Source,BCF>();
        private readonly Dictionary<string,Mesh> meshes=new Dictionary<string,Mesh>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string,TS1MaterialProvider.Material> textures=new Dictionary<string,TS1MaterialProvider.Material>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> used=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public readonly List<Person> People=new List<Person>();
        public IEnumerable<string> UsedFiles {get{return used.OrderBy(x=>x,StringComparer.Ordinal).ToArray();}}
        private long pixelBytes;private int totalVertices;private bool disposed;
        private static string Key(string name) {
            name=Path.GetFileName(name.Replace('\\','/'));
            if(string.IsNullOrWhiteSpace(name)||name.Contains("..")||name.IndexOfAny(new[]{'/', '\\', ':'})>=0)throw new InvalidDataException("Invalid avatar asset name.");
            return name;
        }
        private static IEnumerable<string> Files(string directory) {
            if(!Directory.Exists(directory)||(File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)yield break;
            foreach(var f in Directory.GetFiles(directory))if((File.GetAttributes(f)&FileAttributes.ReparsePoint)==0)yield return f;
            foreach(var child in Directory.GetDirectories(directory))foreach(var f in Files(child))yield return f;
        }
        private static bool Asset(string name) {return new[]{".bcf",".bmf",".cfp",".bmp"}.Contains(Path.GetExtension(name).ToLowerInvariant());}
        public TS1SimVisuals(GamePaths paths,OBJM map,TS1ObjectProvider objects)
        {
            try {
                var saved=map.ObjectData.Values.Where(x=>x.Type==OBJDType.Person).OrderBy(x=>x.ObjectID).ToArray();
                if(saved.Length>32)throw new InvalidDataException("Saved Sim count exceeds visual budget.");
                if(saved.Length==0)return;
                foreach(var root in new[]{"GameData/Animation","GameData/Textures","GameData/Skins","Deluxe","ExpansionShared","ExpansionPack","ExpansionPack2","ExpansionPack3","ExpansionPack4","ExpansionPack5","ExpansionPack6","ExpansionPack7","Downloads"}) {
                    var files=Files(paths.GetGameDataPath(root)).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ThenBy(x=>x,StringComparer.Ordinal).ToArray();
                    foreach(var f in files.Where(x=>Path.GetExtension(x).Equals(".far",StringComparison.OrdinalIgnoreCase))) {
                        var archive=new FAR1Archive(f,false);
                        try {foreach(var e in archive.GetAllFarEntries().Where(x=>Asset(x.Filename)).OrderBy(x=>x.Filename,StringComparer.Ordinal)) {
                            if(e.DataLength==0)continue; // Authored expansion archives include empty placeholders.
                            if(e.DataLength<0||e.DataLength>16*1024*1024||e.DataOffset<0||(long)e.DataOffset+e.DataLength>new FileInfo(f).Length)throw new InvalidDataException("Invalid avatar archive entry: "+f+"!"+e.Filename+" length="+e.DataLength+" offset="+e.DataOffset);
                            sources[Key(e.Filename)]=new Source {File=f,Entry=e.Filename,Length=e.DataLength};
                        }}finally{archive.Close();}
                    }
                    foreach(var f in files.Where(Asset))sources[Key(f)]=new Source {File=f,Length=checked((int)new FileInfo(f).Length)};
                }
                if(sources.Count>32768)throw new InvalidDataException("Avatar catalog exceeds bounds.");
                foreach(var pair in sources.Where(x=>x.Key.EndsWith(".bcf",StringComparison.OrdinalIgnoreCase)).OrderBy(x=>x.Key,StringComparer.Ordinal)) {
                    BCF b;try {using(var stream=new MemoryStream(pair.Value.Read(),false))b=new BCF(stream);} catch(Exception ex){throw new InvalidDataException("Avatar metadata: "+pair.Value.File+"!"+pair.Value.Entry,ex);}
                    foreach(var a in b.Appearances)appearances[a.Name]=pair.Value;
                    foreach(var s in b.Skeletons)skeletons[s.Name]=pair.Value;
                    foreach(var a in b.Animations)animations[a.Name]=pair.Value;
                }
                foreach(var item in saved) {
                    if(item.ContainerID!=0||item.ParentID!=0||item.SavedX<0)throw new NotSupportedException("Saved Sim containment is outside this visual milestone.");
                    var definition=objects.GetObject(item.GUID,true);var strings=definition.Resource.Get<STR>(definition.OBJ.BodyStringID);
                    used.Add(paths.GetGameDataPath(objects.GetSourceFile(item.GUID)));
                    string kind=strings==null?null:strings.GetString(0);
                    if(kind!="adult"&&kind!="child")throw new NotSupportedException("Saved Sim type is not yet supported: "+kind);
                    var visual=map.ReadPersonVisual(item);
                    Source skelSource;if(!skeletons.TryGetValue(kind,out skelSource))throw new FileNotFoundException("Missing TS1 skeleton: "+kind);
                    var skel=BCF(skelSource).Skeletons.First(x=>x.Name.Equals(kind,StringComparison.OrdinalIgnoreCase)).Clone();
                    var avatar=new SimAvatar(skel);
                    ApplyPose(avatar,visual.BaseAnimation);ApplyPose(avatar,visual.Animation);ApplyPose(avatar,visual.CarryAnimation);
                    avatar.Skeleton.ComputeBonePositions(avatar.Skeleton.RootBone,Matrix.Identity);
                    var person=new Person {ID=(short)item.ObjectID,GUID=item.GUID,Name=item.Name,Kind=kind,Direction=item.Direction,
                        Position=new Vector3(item.SavedX/16f,item.SavedY/16f,(item.SavedLevel-1)*TS1LotRenderData.StoryHeight),Appearance=visual};
                    AddPart(person,avatar.Skeleton,visual.Body,visual.BodyTexture);AddPart(person,avatar.Skeleton,visual.Head,visual.HeadTexture);
                    AddPart(person,avatar.Skeleton,visual.LeftHand,visual.LeftHandTexture);AddPart(person,avatar.Skeleton,visual.RightHand,visual.RightHandTexture);
                    foreach(var accessory in visual.Accessories)AddPart(person,avatar.Skeleton,accessory,null);
                    People.Add(person);
                }
            }catch{Dispose();throw;}
        }
        private BCF BCF(Source source) {
            BCF b;if(!bcfs.TryGetValue(source,out b)){using(var stream=new MemoryStream(source.Read(),false))b=new BCF(stream);bcfs.Add(source,b);}used.Add(source.File);return b;
        }
        private Source Require(string name) {Source s;if(!sources.TryGetValue(Key(name),out s))throw new FileNotFoundException("Missing authored Sim asset: "+name);used.Add(s.File);return s;}
        private void ApplyPose(SimAvatar avatar,string state) {
            if(string.IsNullOrEmpty(state))return;var fields=state.Split(';');int frame,weight;
            if(fields.Length!=8||!int.TryParse(fields[3],NumberStyles.Integer,CultureInfo.InvariantCulture,out frame)||!int.TryParse(fields[4],NumberStyles.Integer,CultureInfo.InvariantCulture,out weight)||frame<0||weight<0||weight>1000)
                throw new InvalidDataException("Invalid saved Sim animation state.");
            Source host;if(!animations.TryGetValue(fields[0],out host))throw new FileNotFoundException("Missing saved Sim animation: "+fields[0]);
            var animation=BCF(host).Animations.First(x=>x.Name.Equals(fields[0],StringComparison.OrdinalIgnoreCase));
            if(animation.Translations==null){var cfp=new CFP();using(var stream=new MemoryStream(Require(animation.XSkillName+".cfp").Read(),false))cfp.Read(stream);cfp.EnrichAnim(animation);}
            // Saved frame is normalized in thousandths, not an absolute frame index.
            float time=frame/1000f*Math.Max(0,animation.NumFrames-1);
            if(time>animation.NumFrames)throw new InvalidDataException("Saved animation frame outside animation.");
            foreach(var m in animation.Motions)if(m.FrameCount==0||m.FrameCount>262144||
                (m.HasTranslation&&(long)m.FirstTranslationIndex+m.FrameCount>animation.TranslationCount)||
                (m.HasRotation&&(long)m.FirstRotationIndex+m.FrameCount>animation.RotationCount))throw new InvalidDataException("Invalid saved animation channels.");
            Animator.RenderFrame(avatar,animation,(int)time,time-(int)time,weight/1000f);
        }
        private void AddPart(Person person,Skeleton skeleton,string appearance,string binding) {
            if(string.IsNullOrEmpty(appearance))return;
            Source host;if(!appearances.TryGetValue(appearance,out host))throw new FileNotFoundException("Missing saved Sim appearance: "+appearance);
            var skin=BCF(host).Appearances.First(x=>x.Name.Equals(appearance,StringComparison.OrdinalIgnoreCase));
            foreach(var b in skin.Bindings) {
                string meshName=b.RealBinding.MeshName+".bmf";Mesh mesh;
                if(!meshes.TryGetValue(meshName,out mesh)){mesh=new Mesh();using(var stream=new MemoryStream(Require(meshName).Read(),false))mesh.Read(stream,true);meshes.Add(meshName,mesh);}
                if(!string.IsNullOrEmpty(binding)&&binding.IndexOf('=')<1)throw new InvalidDataException("Invalid saved Sim texture binding.");
                string texture=string.IsNullOrEmpty(binding)?mesh.TextureName:binding.Substring(binding.IndexOf('=')+1);
                string textureName=texture+".bmp";TS1MaterialProvider.Material pixels;
                if(!textures.TryGetValue(textureName,out pixels)) {
                    using(var stream=new MemoryStream(Require(textureName).Read(),false))pixels=TS1MaterialProvider.DecodeRoofBitmap(stream,textureName);
                    long bytes=(long)pixels.Pixels.Length*4;if(pixelBytes+bytes>16*1024*1024)throw new InvalidDataException("Sim texture cache exceeds budget.");pixelBytes+=bytes;textures.Add(textureName,pixels);
                }
                if(totalVertices>262144-mesh.TriangleVertexCount)throw new InvalidDataException("Sim geometry exceeds scene budget.");
                var vertices=mesh.BakePose(skeleton);totalVertices=checked(totalVertices+vertices.Length);
                foreach(var vertex in vertices)if(float.IsNaN(vertex.Position.LengthSquared())||float.IsInfinity(vertex.Position.LengthSquared())||vertex.Position.LengthSquared()>400)throw new InvalidDataException("Invalid Sim pose bounds.");
                person.Parts.Add(new Part {Texture=pixels,Vertices=vertices});
            }
        }
        public void Dispose(){if(disposed)return;disposed=true;People.Clear();sources.Clear();appearances.Clear();skeletons.Clear();animations.Clear();bcfs.Clear();meshes.Clear();textures.Clear();used.Clear();}
    }
}
