/*
 * This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
 * If a copy of the MPL was not distributed with this file, You can obtain one at
 * http://mozilla.org/MPL/2.0/.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using FSO.Common.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FSO.Files.Utils;
using FSO.Common.Rendering.Framework;
using FSO.Vitaboy.Model;


namespace FSO.Vitaboy
{
    /// <summary>
    /// 3D Mesh.
    /// </summary>
    public class Mesh : I3DGeometry
    {
        /** 3D Data **/
        public VitaboyVertex[] VertexBuffer;

        private int[] BlendVertBoneIndices;
        private Vector3[] BlendVerts;
        private Vector3[] BlendNormals;

        protected short[] IndexBuffer;
        protected int NumPrimitives;
        public int TriangleVertexCount {get{return IndexBuffer==null?0:IndexBuffer.Length;}}
        public BoneBinding[] BoneBindings;
        public BlendData[] BlendData;

        private bool GPUMode;
        private DynamicVertexBuffer GPUVertexBuffer;
        private IndexBuffer GPUIndexBuffer;
        private bool Prepared = false;

        public string SkinName;
        public string TextureName;

        public Mesh()
        {
        }

        /// <summary>
        /// Clones this mesh.
        /// </summary>
        /// <returns>A Mesh instance with the same data as this one.</returns>
        public Mesh Clone()
        {
            var result = new Mesh()
            {
                BlendData = BlendData,
                BoneBindings = BoneBindings,
                NumPrimitives = NumPrimitives,
                IndexBuffer = IndexBuffer,
                VertexBuffer = VertexBuffer,
                BlendVerts = BlendVerts,
                BlendNormals = BlendNormals,
                BlendVertBoneIndices = (int[])BlendVertBoneIndices.Clone()
            };
            return result;
        }

        /// <summary>
        /// Transforms the verticies making up this mesh into
        /// the designated bone positions.
        /// </summary>
        /// <param name="bone">The bone to start with. Should always be the ROOT bone.</param>
        ///

        public void Prepare(Bone bone)
        //TODO: assumes that skeleton will be same configuration for all bindings of this mesh.
        //If any meshes are used by pets and avatars(???) or we implement children this will need
        //to be changed to support binds to multiple SKEL bases.
        {
            if (Prepared) return;
            var binding = BoneBindings.FirstOrDefault(x => x.BoneName.Equals(bone.Name, StringComparison.InvariantCultureIgnoreCase));
            if (binding != null)
            {
                for (var i = 0; i < binding.RealVertexCount; i++)
                {
                    var vertexIndex = binding.FirstRealVertex + i;
                    VertexBuffer[vertexIndex].Parameters.X = bone.Index;
                }

                for (var i = 0; i < binding.BlendVertexCount; i++)
                {
                    var blendVertexIndex = binding.FirstBlendVertex + i;
                    BlendVertBoneIndices[blendVertexIndex] = bone.Index;

                }
            }

            foreach (var child in bone.Children)
            {
                Prepare(child);
            }

            if (bone.Name.Equals("ROOT", StringComparison.InvariantCultureIgnoreCase))
            {
                for (int i = 0; i < BlendData.Length; i++)
                {
                    var data = BlendData[i];
                    var vert = BlendVertBoneIndices[i];

                    VertexBuffer[data.OtherVertex].Parameters.Y = BlendVertBoneIndices[i];
                    VertexBuffer[data.OtherVertex].Parameters.Z = data.Weight;
                    VertexBuffer[data.OtherVertex].BvPosition = BlendVerts[i];
                }

                InvalidateMesh();
                Prepared = true;
            }
        }

        public void StoreOnGPU(GraphicsDevice device)
        {
            GPUMode = true;
            GPUVertexBuffer = new DynamicVertexBuffer(device, typeof(VitaboyVertex), VertexBuffer.Length, BufferUsage.None);
            GPUVertexBuffer.SetData(VertexBuffer);

            GPUIndexBuffer = new IndexBuffer(device, IndexElementSize.SixteenBits, IndexBuffer.Length, BufferUsage.None);
            GPUIndexBuffer.SetData(IndexBuffer);
        }

        public void InvalidateMesh()
        {
            if (GPUMode)
            {
                GPUVertexBuffer.SetData(VertexBuffer);
            }
        }

        #region I3DGeometry Members

        public void DrawGeometry(GraphicsDevice gd){
            if (GPUMode){
                gd.Indices = GPUIndexBuffer;
                gd.SetVertexBuffer(GPUVertexBuffer);
                gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, VertexBuffer.Length, 0, NumPrimitives);
            }else{
                gd.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, VertexBuffer, 0, VertexBuffer.Length, IndexBuffer, 0, NumPrimitives);
            }
        }

        #endregion

        /// <summary>
        /// Draws this mesh.
        /// </summary>
        /// <param name="gd">A GraphicsDevice instance used for drawing.</param>
        public void Draw(GraphicsDevice gd){
            if (!GPUMode) StoreOnGPU(gd);
            gd.Indices = GPUIndexBuffer;
            gd.SetVertexBuffer(GPUVertexBuffer);
            gd.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0,0, 0, 0, NumPrimitives);
        }

        /// <summary>
        /// Reads a mesh from a stream.
        /// </summary>
        /// <param name="stream">A Stream instance holding a mesh.</param>
        public void Read(Stream stream, bool bmf)
        {
            using (var io = IoBuffer.FromStream(stream, bmf ? ByteOrder.LITTLE_ENDIAN : ByteOrder.BIG_ENDIAN))
            {
                if (bmf)
                {
                    SkinName = io.ReadPascalString();
                    TextureName = io.ReadPascalString();
                }
                else
                {
                    var version = io.ReadInt32();
                }
                var boneCount = io.ReadBoundedCount(256);
                var boneNames = new string[boneCount];
                for (var i = 0; i < boneCount; i++){
                    boneNames[i] = io.ReadPascalString();
                }

                var faceCount = io.ReadBoundedCount(16384,12);
                NumPrimitives = faceCount;

                IndexBuffer = new short[faceCount * 3];
                int offset = 0;
                for (var i = 0; i < faceCount; i++){
                    for(int j=0;j<3;j++) { int index=io.ReadInt32();if(index<0||index>32767)throw new InvalidDataException("Invalid avatar face index.");IndexBuffer[offset++]=(short)index; }
                }

                /** Bone bindings **/
                var bindingCount = io.ReadBoundedCount(256,20);
                BoneBindings = new BoneBinding[bindingCount];
                for (var i = 0; i < bindingCount; i++)
                {
                    BoneBindings[i] = new BoneBinding
                    {
                        BoneIndex = io.ReadInt32(),
                        FirstRealVertex = io.ReadInt32(),
                        RealVertexCount = io.ReadInt32(),
                        FirstBlendVertex = io.ReadInt32(),
                        BlendVertexCount = io.ReadInt32()
                    };

                    if(BoneBindings[i].BoneIndex<0||BoneBindings[i].BoneIndex>=boneCount)throw new InvalidDataException("Invalid avatar bone index.");
                    BoneBindings[i].BoneName = boneNames[BoneBindings[i].BoneIndex];
                }


                var realVertexCount = io.ReadBoundedCount(32768,8);
                VertexBuffer = new VitaboyVertex[realVertexCount];

                for (var i = 0; i < realVertexCount; i++){
                    VertexBuffer[i].TextureCoordinate.X = io.ReadFloat();
                    VertexBuffer[i].TextureCoordinate.Y = io.ReadFloat();
                }

                /** Blend data **/
                var blendVertexCount = io.ReadBoundedCount(32768,8);
                BlendData = new BlendData[blendVertexCount];
                for (var i = 0; i < blendVertexCount; i++)
                {
                    BlendData[i] = new BlendData
                    {
                        Weight = (float)io.ReadInt32() / 0x8000,
                        OtherVertex = io.ReadInt32()
                    };
                }

                var realVertexCount2 = io.ReadInt32();
                if(realVertexCount2!=realVertexCount+blendVertexCount)throw new InvalidDataException("Mismatched avatar vertex count: "+SkinName+" real="+realVertexCount+" blend="+blendVertexCount+" total="+realVertexCount2);

                for (int i = 0; i < realVertexCount; i++)
                {
                    VertexBuffer[i].Position = new Microsoft.Xna.Framework.Vector3(
                        -io.ReadFloat(),
                        io.ReadFloat(),
                        io.ReadFloat()
                    );

                    VertexBuffer[i].Normal = new Microsoft.Xna.Framework.Vector3(
                        -io.ReadFloat(),
                        io.ReadFloat(),
                        io.ReadFloat()
                    );
                }

                BlendVerts = new Vector3[blendVertexCount];
                BlendNormals = new Vector3[blendVertexCount];

                for (int i = 0; i < blendVertexCount; i++)
                {
                    BlendVerts[i] = new Vector3(
                        -io.ReadFloat(),
                        io.ReadFloat(),
                        io.ReadFloat()
                    );

                    BlendNormals[i] = new Vector3(
                        -io.ReadFloat(),
                        io.ReadFloat(),
                        io.ReadFloat()
                    );
                }

                BlendVertBoneIndices = new int[blendVertexCount];
            }
        }
        // CPU pose snapshot: never Prepare/mutate a cached mesh for a different skeleton.
        public VertexPositionNormalTexture[] BakePose(Skeleton skeleton)
        {
            var real=new VertexPositionNormalTexture[VertexBuffer.Length];
            var assigned=new bool[real.Length];var blended=new bool[BlendVerts.Length];
            foreach(var binding in BoneBindings) {
                var bone=skeleton.Bones.FirstOrDefault(x=>string.Equals(x.Name,binding.BoneName,StringComparison.OrdinalIgnoreCase));
                if(bone==null)throw new InvalidDataException("Missing avatar bone: "+binding.BoneName);
                if(binding.RealVertexCount<0||binding.BlendVertexCount<0||
                    (binding.RealVertexCount>0&&(binding.FirstRealVertex<0||binding.FirstRealVertex>real.Length-binding.RealVertexCount))||
                    (binding.BlendVertexCount>0&&(binding.FirstBlendVertex<0||binding.FirstBlendVertex>blended.Length-binding.BlendVertexCount)))
                    throw new InvalidDataException("Avatar binding outside mesh.");
                for(int i=binding.FirstRealVertex;i<binding.FirstRealVertex+binding.RealVertexCount;i++) {
                    if(assigned[i])throw new InvalidDataException("Overlapping avatar binding.");assigned[i]=true;
                    var v=VertexBuffer[i];real[i]=new VertexPositionNormalTexture(Vector3.Transform(v.Position,bone.AbsoluteMatrix),Vector3.TransformNormal(v.Normal,bone.AbsoluteMatrix),v.TextureCoordinate);
                }
            }
            foreach(var binding in BoneBindings) {
                var bone=skeleton.Bones.First(x=>string.Equals(x.Name,binding.BoneName,StringComparison.OrdinalIgnoreCase));
                for(int i=binding.FirstBlendVertex;i<binding.FirstBlendVertex+binding.BlendVertexCount;i++) {
                    if(blended[i])throw new InvalidDataException("Overlapping avatar blend binding.");blended[i]=true;
                    var b=BlendData[i];if(b.OtherVertex<0||b.OtherVertex>=real.Length||!assigned[b.OtherVertex]||b.Weight<0||b.Weight>1)
                        throw new InvalidDataException("Invalid avatar blend vertex.");
                    var v=real[b.OtherVertex];v.Position=Vector3.Lerp(v.Position,Vector3.Transform(BlendVerts[i],bone.AbsoluteMatrix),b.Weight);
                    v.Normal=Vector3.Lerp(v.Normal,Vector3.TransformNormal(BlendNormals[i],bone.AbsoluteMatrix),b.Weight);real[b.OtherVertex]=v;
                }
            }
            if(assigned.Any(x=>!x)||blended.Any(x=>!x))throw new InvalidDataException("Unbound avatar vertices.");
            var triangles=new VertexPositionNormalTexture[IndexBuffer.Length];
            for(int i=0;i<triangles.Length;i++){int index=IndexBuffer[i];if(index<0||index>=real.Length)throw new InvalidDataException("Avatar index outside vertices.");triangles[i]=real[index];}
            return triangles;
        }
    }
}
