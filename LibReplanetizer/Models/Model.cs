﻿// Copyright (C) 2018-2021, The Replanetizer Contributors.
// Replanetizer is free software: you can redistribute it
// and/or modify it under the terms of the GNU General Public
// License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// Please see the LICENSE.md file for more details.

using System.ComponentModel;
using LibReplanetizer.LevelObjects;
using System.Collections.Generic;
using System.IO;
using System;
using static LibReplanetizer.DataFunctions;

namespace LibReplanetizer.Models
{
    /*
        General purpose 3D model used for rendering
    */

    public abstract class Model : IRenderable
    {
        public short id { get; set; }
        [Category("Attributes"), DisplayName("Size")]
        public float size { get; set; } = 1.0f;
        public float[] vertexBuffer = { };
        public ushort[] indexBuffer = { };

        // Every vertex can be assigned to at most 4 bones
        // weights contains 4 uint8 each being of weight (value / 255.0)
        // ids contains 4 uint8 each defining which bones we refer to
        [Category("Attributes"), DisplayName("Bone Weights")]
        public uint[] weights { get; set; } = new uint[0];
        [Category("Attributes"), DisplayName("Vertex to Bone Map")]
        public uint[] ids { get; set; } = new uint[0];

        [Category("Attributes"), DisplayName("Vertex Colors")]
        public byte[] rgbas { get; set; } = new byte[0];

        [Category("Attributes"), DisplayName("Texture Configurations")]
        public List<TextureConfig> textureConfig { get; set; } = new List<TextureConfig>();

        public ushort[] GetIndices()
        {
            return indexBuffer;
        }

        public float[] GetVertices()
        {
            return vertexBuffer;
        }

        public bool IsDynamic()
        {
            return false;
        }

        protected int GetFaceCount()
        {
            int faceCount = 0;
            if (textureConfig != null)
            {
                foreach (TextureConfig tex in textureConfig)
                {
                    faceCount += tex.size;
                }
            }
            return faceCount;
        }

        //Get texture configs of different types using elemsize
        public static List<TextureConfig> GetTextureConfigs(FileStream fs, int texturePointer, int textureCount, int elemSize, bool negate = false)
        {
            int idOffset = 0, startOffset = 0, sizeOffset = 0, modeOffset = 0;

            switch (elemSize)
            {
                case 0x10:
                    idOffset = 0x00;
                    startOffset = 0x04;
                    sizeOffset = 0x08;
                    modeOffset = 0x0C;
                    break;
                case 0x18:
                    idOffset = 0x00;
                    startOffset = 0x08;
                    sizeOffset = 0x0C;
                    modeOffset = 0x14;
                    break;
            }

            var textureConfigs = new List<TextureConfig>();
            byte[] texBlock = ReadBlock(fs, texturePointer, textureCount * elemSize);
            int neg = 0;
            for (int i = 0; i < textureCount; i++)
            {
                TextureConfig textureConfig = new TextureConfig();
                textureConfig.id = ReadInt(texBlock, (i * elemSize) + idOffset);
                textureConfig.start = ReadInt(texBlock, (i * elemSize) + startOffset);
                textureConfig.size = ReadInt(texBlock, (i * elemSize) + sizeOffset);
                textureConfig.mode = ReadInt(texBlock, (i * elemSize) + modeOffset);
                if (negate)
                {
                    if (i == 0) neg = textureConfig.start;
                    textureConfig.start -= neg;
                }

                textureConfigs.Add(textureConfig);
            }
            return textureConfigs;
        }

        //Get vertices with UV's baked in
        public float[] GetVertices(FileStream fs, int vertexPointer, int vertexCount, int elemSize)
        {
            float[] vertexBuffer = new float[vertexCount * 8];
            weights = new uint[vertexCount];
            ids = new uint[vertexCount];
            //List<float> vertexBuffer = new List<float>();
            byte[] vertBlock = ReadBlock(fs, vertexPointer, vertexCount * elemSize);
            for (int i = 0; i < vertexCount; i++)
            {
                vertexBuffer[(i * 8) + 0] = (ReadFloat(vertBlock, (i * elemSize) + 0x00));    //VertexX
                vertexBuffer[(i * 8) + 1] = (ReadFloat(vertBlock, (i * elemSize) + 0x04));    //VertexY
                vertexBuffer[(i * 8) + 2] = (ReadFloat(vertBlock, (i * elemSize) + 0x08));    //VertexZ
                vertexBuffer[(i * 8) + 3] = (ReadFloat(vertBlock, (i * elemSize) + 0x0C));    //NormX
                vertexBuffer[(i * 8) + 4] = (ReadFloat(vertBlock, (i * elemSize) + 0x10));    //NormY
                vertexBuffer[(i * 8) + 5] = (ReadFloat(vertBlock, (i * elemSize) + 0x14));    //NormZ
                vertexBuffer[(i * 8) + 6] = (ReadFloat(vertBlock, (i * elemSize) + 0x18));    //UVu
                vertexBuffer[(i * 8) + 7] = (ReadFloat(vertBlock, (i * elemSize) + 0x1C));    //UVv
                if (elemSize == 0x28)
                {
                    weights[i] = (ReadUint(vertBlock, (i * elemSize) + 0x20));
                    ids[i] = (ReadUint(vertBlock, (i * elemSize) + 0x24));
                }


            }
            return vertexBuffer;
        }

        public byte[] SerializeVertices()
        {
            int elemSize = 0x28;
            byte[] outBytes = new byte[(vertexBuffer.Length / 8) * elemSize];

            for (int i = 0; i < vertexBuffer.Length / 8; i++)
            {
                WriteFloat(outBytes, (i * elemSize) + 0x00, vertexBuffer[(i * 8) + 0]);
                WriteFloat(outBytes, (i * elemSize) + 0x04, vertexBuffer[(i * 8) + 1]);
                WriteFloat(outBytes, (i * elemSize) + 0x08, vertexBuffer[(i * 8) + 2]);
                WriteFloat(outBytes, (i * elemSize) + 0x0C, vertexBuffer[(i * 8) + 3]);
                WriteFloat(outBytes, (i * elemSize) + 0x10, vertexBuffer[(i * 8) + 4]);
                WriteFloat(outBytes, (i * elemSize) + 0x14, vertexBuffer[(i * 8) + 5]);
                WriteFloat(outBytes, (i * elemSize) + 0x18, vertexBuffer[(i * 8) + 6]);
                WriteFloat(outBytes, (i * elemSize) + 0x1C, vertexBuffer[(i * 8) + 7]);
                WriteUint(outBytes, (i * elemSize) + 0x20, weights[i]);
                WriteUint(outBytes, (i * elemSize) + 0x24, ids[i]);
            }

            return outBytes;
        }



        public byte[] SerializeTieVertices()
        {
            int elemSize = 0x18;
            byte[] outBytes = new byte[(vertexBuffer.Length / 8) * elemSize];

            for (int i = 0; i < vertexBuffer.Length / 8; i++)
            {
                WriteFloat(outBytes, (i * elemSize) + 0x00, vertexBuffer[(i * 8) + 0]);
                WriteFloat(outBytes, (i * elemSize) + 0x04, vertexBuffer[(i * 8) + 1]);
                WriteFloat(outBytes, (i * elemSize) + 0x08, vertexBuffer[(i * 8) + 2]);
                WriteFloat(outBytes, (i * elemSize) + 0x0C, vertexBuffer[(i * 8) + 3]);
                WriteFloat(outBytes, (i * elemSize) + 0x10, vertexBuffer[(i * 8) + 4]);
                WriteFloat(outBytes, (i * elemSize) + 0x14, vertexBuffer[(i * 8) + 5]);
            }

            return outBytes;
        }

        public byte[] SerializeUVs()
        {
            int elemSize = 0x08;
            byte[] outBytes = new byte[(vertexBuffer.Length / 8) * elemSize];

            for (int i = 0; i < vertexBuffer.Length / 8; i++)
            {
                WriteFloat(outBytes, (i * elemSize) + 0x00, vertexBuffer[(i * 8) + 6]);
                WriteFloat(outBytes, (i * elemSize) + 0x04, vertexBuffer[(i * 8) + 7]);
            }

            return outBytes;
        }

        //Get vertices with UV's baked in, but no normals
        public static float[] GetVerticesSkybox(FileStream fs, int vertexPointer, int vertexCount)
        {
            float[] vertexBuffer = new float[vertexCount * 6];

            //List<float> vertexBuffer = new List<float>();
            byte[] vertBlock = ReadBlock(fs, vertexPointer, vertexCount * 0x18);
            for (int i = 0; i < vertexCount; i++)
            {
                vertexBuffer[(i * 6) + 0] = ReadFloat(vertBlock, i * 0x18 + 0x00);              //VertexX
                vertexBuffer[(i * 6) + 1] = ReadFloat(vertBlock, i * 0x18 + 0x04);              //VertexY
                vertexBuffer[(i * 6) + 2] = ReadFloat(vertBlock, i * 0x18 + 0x08);              //VertexZ
                vertexBuffer[(i * 6) + 3] = ReadFloat(vertBlock, i * 0x18 + 0x0C);              //UVu
                vertexBuffer[(i * 6) + 4] = ReadFloat(vertBlock, i * 0x18 + 0x10);              //UVv
                vertexBuffer[(i * 6) + 5] = BitConverter.ToSingle(vertBlock, i * 0x18 + 0x14);  //Actually vertexcolors
            }
            return vertexBuffer;
        }

        public static byte[] GetVertexBytesSkybox(float[] vertexBuffer)
        {
            int vertexCount = vertexBuffer.Length / 6;
            byte[] vertexBytes = new byte[vertexCount * 0x18];

            for (int i = 0; i < vertexCount; i++)
            {
                WriteFloat(vertexBytes, (i * 0x18) + 0x00, vertexBuffer[(i * 6) + 0]);
                WriteFloat(vertexBytes, (i * 0x18) + 0x04, vertexBuffer[(i * 6) + 1]);
                WriteFloat(vertexBytes, (i * 0x18) + 0x08, vertexBuffer[(i * 6) + 2]);
                WriteFloat(vertexBytes, (i * 0x18) + 0x0C, vertexBuffer[(i * 6) + 3]);
                WriteFloat(vertexBytes, (i * 0x18) + 0x10, vertexBuffer[(i * 6) + 4]);
                BitConverter.GetBytes(vertexBuffer[(i * 6) + 5]).CopyTo(vertexBytes, (i * 0x18) + 0x14);
            }
            return vertexBytes;
        }

        public byte[] GetFaceBytes(ushort offset = 0)
        {
            byte[] indexBytes = new byte[indexBuffer.Length * sizeof(ushort)];
            for (int i = 0; i < indexBuffer.Length; i++)
            {
                WriteUshort(indexBytes, i * sizeof(ushort), (ushort) (indexBuffer[i] + offset));
            }
            return indexBytes;
        }

        //Get vertices with UV's located somewhere else
        public static float[] GetVertices(FileStream fs, int vertexPointer, int uvPointer, int vertexCount, int vertexElemSize, int uvElemSize)
        {
            float[] vertexBuffer = new float[vertexCount * 8];

            byte[] vertBlock = ReadBlock(fs, vertexPointer, vertexCount * vertexElemSize);
            byte[] uvBlock = ReadBlock(fs, uvPointer, vertexCount * uvElemSize);
            for (int i = 0; i < vertexCount; i++)
            {
                vertexBuffer[(i * 8) + 0] = (ReadFloat(vertBlock, (i * vertexElemSize) + 0x00));    //VertexX
                vertexBuffer[(i * 8) + 1] = (ReadFloat(vertBlock, (i * vertexElemSize) + 0x04));    //VertexY
                vertexBuffer[(i * 8) + 2] = (ReadFloat(vertBlock, (i * vertexElemSize) + 0x08));    //VertexZ
                vertexBuffer[(i * 8) + 3] = (ReadFloat(vertBlock, (i * vertexElemSize) + 0x0C));    //NormX
                vertexBuffer[(i * 8) + 4] = (ReadFloat(vertBlock, (i * vertexElemSize) + 0x10));    //NormY
                vertexBuffer[(i * 8) + 5] = (ReadFloat(vertBlock, (i * vertexElemSize) + 0x14));    //NormZ

                vertexBuffer[(i * 8) + 6] = (ReadFloat(uvBlock, (i * uvElemSize) + 0x00));    //UVu
                vertexBuffer[(i * 8) + 7] = (ReadFloat(uvBlock, (i * uvElemSize) + 0x04));    //UVv
            }
            return vertexBuffer;
        }

        //Get indices
        public static ushort[] GetIndices(FileStream fs, int indexPointer, int faceCount, int offset = 0)
        {
            ushort[] indexBuffer = new ushort[faceCount];
            byte[] indexBlock = ReadBlock(fs, indexPointer, faceCount * sizeof(ushort));

            for (int i = 0; i < faceCount; i++)
            {
                ushort face = ReadUshort(indexBlock, i * sizeof(ushort));
                indexBuffer[i] = (ushort) (face - offset);
            }

            return indexBuffer;
        }

    }
}
