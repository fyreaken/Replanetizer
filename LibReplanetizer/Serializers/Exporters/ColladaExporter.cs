// Copyright (C) 2018-2022, The Replanetizer Contributors.
// Replanetizer is free software: you can redistribute it
// and/or modify it under the terms of the GNU General Public
// License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// Please see the LICENSE.md file for more details.

using LibReplanetizer.LevelObjects;
using LibReplanetizer.Models;
using LibReplanetizer.Models.Animations;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.IO;

namespace LibReplanetizer
{
    public class ColladaExporter : Exporter
    {
        private ExporterModelSettings settings = new ExporterModelSettings();

        public ColladaExporter()
        {
        }

        public ColladaExporter(ExporterModelSettings settings)
        {
            this.settings = settings;
        }

        public override string GetFileEnding()
        {
            return ".dae";
        }

        /*
         * Blender removes bones if their length is too small. However,
         * in RaC bones may be of length 0 so we shift bones by some
         * minimal amount to avoid the Blender behaviour while keeping
         * the error small.
         */
        private static readonly float BLENDER_BONE_MIN_LENGTH = 0.000001f;
        private static readonly float BLENDER_BONE_FIX = 0.001f;

        private void WriteSkeleton(StreamWriter colladaStream, Skeleton skeleton, float size, string indent = "")
        {
            Matrix3x4 trans = skeleton.bone.transformation;
            Matrix3 orthoTrans = new Matrix3(trans.Row0.Xyz, trans.Row1.Xyz, trans.Row2.Xyz);
            Matrix3 relOrthoTrans = orthoTrans;

            // We need to represent our transformation relative to the parent node
            if (skeleton.parent != null)
            {
                Matrix3x4 matP = skeleton.parent.bone.transformation;
                Matrix3 matPTrans = new Matrix3(matP.Row0.Xyz, matP.Row1.Xyz, matP.Row2.Xyz);
                matPTrans.Transpose();
                relOrthoTrans = matPTrans * relOrthoTrans;
            }
            orthoTrans.Transpose();
            Vector3 tip = new Vector3(trans.M14, trans.M24, trans.M34);
            tip *= size / 1024.0f;
            tip = orthoTrans * tip;

            colladaStream.WriteLine(indent + "<node id=\"Skel" + skeleton.bone.id.ToString() + "\" sid=\"J" + skeleton.bone.id.ToString() + "\" name=\"Skel" + skeleton.bone.id.ToString() + "\" type=\"JOINT\">");
            colladaStream.Write(indent + "<matrix sid=\"transform\">");
            colladaStream.Write((relOrthoTrans.M11).ToString("G", en_US) + " ");
            colladaStream.Write((relOrthoTrans.M12).ToString("G", en_US) + " ");
            colladaStream.Write((relOrthoTrans.M13).ToString("G", en_US) + " ");
            colladaStream.Write((trans.M14 * size / 1024.0f).ToString("G", en_US) + " ");
            colladaStream.Write((relOrthoTrans.M21).ToString("G", en_US) + " ");
            colladaStream.Write((relOrthoTrans.M22).ToString("G", en_US) + " ");
            colladaStream.Write((relOrthoTrans.M23).ToString("G", en_US) + " ");
            colladaStream.Write((trans.M24 * size / 1024.0f).ToString("G", en_US) + " ");
            colladaStream.Write((relOrthoTrans.M31).ToString("G", en_US) + " ");
            colladaStream.Write((relOrthoTrans.M32).ToString("G", en_US) + " ");
            colladaStream.Write((relOrthoTrans.M33).ToString("G", en_US) + " ");
            colladaStream.Write((trans.M34 * size / 1024.0f).ToString("G", en_US) + " ");
            colladaStream.Write("0 ");
            colladaStream.Write("0 ");
            colladaStream.Write("0 ");
            colladaStream.Write("1 ");
            colladaStream.WriteLine("</matrix>");

            if (tip.LengthSquared < BLENDER_BONE_MIN_LENGTH)
            {
                tip.X = 0.0f;
                tip.Y = 0.0f;
                tip.Z = BLENDER_BONE_FIX;
            }

            colladaStream.WriteLine(indent + "<extra>");
            colladaStream.WriteLine(indent + "\t<technique profile=\"blender\">");
            colladaStream.WriteLine(indent + "\t\t<connect>1</connect>");
            colladaStream.WriteLine(indent + "\t\t<layer>0</layer>");
            colladaStream.WriteLine(indent + "\t\t<roll>0</roll>");
            colladaStream.WriteLine(indent + "\t\t<tip_x>" + (tip.X).ToString("G", en_US) + "</tip_x>");
            colladaStream.WriteLine(indent + "\t\t<tip_y>" + (tip.Y).ToString("G", en_US) + "</tip_y>");
            colladaStream.WriteLine(indent + "\t\t<tip_z>" + (tip.Z).ToString("G", en_US) + "</tip_z>");
            colladaStream.WriteLine(indent + "\t</technique>");
            colladaStream.WriteLine(indent + "</extra>");

            foreach (Skeleton child in skeleton.children)
            {
                WriteSkeleton(colladaStream, child, size, indent + "\t");
            }

            colladaStream.WriteLine(indent + "</node>");
        }

        private void WriteAnimation(StreamWriter colladaStream, Animation anim, int boneCount, string name, string indent = "")
        {
            colladaStream.WriteLine(indent + "<animation id=\"" + name + "\" name=\"" + name + "\">");

            string timeString = "";
            float frameStartTime = 0.0f;
            foreach (Frame frame in anim.frames)
            {
                timeString += (frameStartTime).ToString("G", en_US) + " ";
                if (frame.speed == 0.0f)
                {
                    frameStartTime += 1.0f / (60.0f * 0.2f);
                }
                else
                {
                    frameStartTime += 1.0f / (60.0f * frame.speed);
                }
            }

            string interpString = "";
            for (int j = 0; j < anim.frames.Count; j++)
            {
                interpString += "LINEAR ";
            }

            for (int k = 0; k < boneCount; k++)
            {
                colladaStream.WriteLine(indent + "\t<animation id=\"" + name + "_" + k.ToString() + "\" name=\"" + name + "_" + k.ToString() + "\">");
                colladaStream.WriteLine(indent + "\t\t<source id=\"" + name + "_" + k.ToString() + "Input\">");
                colladaStream.Write(indent + "\t\t\t<float_array id=\"" + name + "_" + k.ToString() + "InputArray\" count=\"" + anim.frames.Count + "\">");
                colladaStream.Write(timeString);
                colladaStream.WriteLine(indent + "</float_array>");
                colladaStream.WriteLine(indent + "\t\t\t<technique_common>");
                colladaStream.WriteLine(indent + "\t\t\t\t<accessor source=\"#" + name + "_" + k.ToString() + "InputArray\" count=\"" + anim.frames.Count + "\" stride=\"1\">");
                colladaStream.WriteLine(indent + "\t\t\t\t\t<param name=\"TIME\" type=\"float\"/>");
                colladaStream.WriteLine(indent + "\t\t\t\t</accessor>");
                colladaStream.WriteLine(indent + "\t\t\t</technique_common>");
                colladaStream.WriteLine(indent + "\t\t</source>");
                colladaStream.WriteLine(indent + "\t\t<source id=\"" + name + "_" + k.ToString() + "Output\">");
                colladaStream.Write(indent + "\t\t\t<float_array id=\"" + name + "_" + k.ToString() + "OutputArray\" count=\"" + 16 * anim.frames.Count + "\">");
                foreach (Frame frame in anim.frames)
                {
                    short[] rots = frame.rotations[k];

                    Quaternion quat = new Quaternion((rots[0] / 32767f) * 180f, (rots[1] / 32767f) * 180f, (rots[2] / 32767f) * 180f, (-rots[3] / 32767f) * 180f);

                    Matrix4 rotation = Matrix4.CreateFromQuaternion(quat);
                    Matrix4 animationMatrix = rotation;
                    animationMatrix.Transpose();

                    colladaStream.Write((animationMatrix.M11).ToString("G", en_US) + " ");
                    colladaStream.Write((animationMatrix.M12).ToString("G", en_US) + " ");
                    colladaStream.Write((animationMatrix.M13).ToString("G", en_US) + " ");
                    colladaStream.Write((animationMatrix.M14).ToString("G", en_US) + " ");
                    colladaStream.Write((animationMatrix.M21).ToString("G", en_US) + " ");
                    colladaStream.Write((animationMatrix.M22).ToString("G", en_US) + " ");
                    colladaStream.Write((animationMatrix.M23).ToString("G", en_US) + " ");
                    colladaStream.Write((animationMatrix.M24).ToString("G", en_US) + " ");
                    colladaStream.Write((animationMatrix.M31).ToString("G", en_US) + " ");
                    colladaStream.Write((animationMatrix.M32).ToString("G", en_US) + " ");
                    colladaStream.Write((animationMatrix.M33).ToString("G", en_US) + " ");
                    colladaStream.Write((animationMatrix.M34).ToString("G", en_US) + " ");
                    colladaStream.Write((animationMatrix.M41).ToString("G", en_US) + " ");
                    colladaStream.Write((animationMatrix.M42).ToString("G", en_US) + " ");
                    colladaStream.Write((animationMatrix.M43).ToString("G", en_US) + " ");
                    colladaStream.Write((animationMatrix.M44).ToString("G", en_US) + " ");
                }
                colladaStream.WriteLine("</float_array>");
                colladaStream.WriteLine(indent + "\t\t\t<technique_common>");
                colladaStream.WriteLine(indent + "\t\t\t\t<accessor source=\"#" + name + "_" + k.ToString() + "OutputArray\" count=\"" + anim.frames.Count + "\" stride=\"16\">");
                colladaStream.WriteLine(indent + "\t\t\t\t\t<param name=\"TRANSFORM\" type=\"float4x4\"/>");
                colladaStream.WriteLine(indent + "\t\t\t\t</accessor>");
                colladaStream.WriteLine(indent + "\t\t\t</technique_common>");
                colladaStream.WriteLine(indent + "\t\t</source>");
                colladaStream.WriteLine(indent + "\t\t<source id=\"" + name + "_" + k.ToString() + "Interp\">");
                colladaStream.Write(indent + "\t\t\t<Name_array id=\"" + name + "_" + k.ToString() + "InterpArray\" count=\"" + anim.frames.Count + "\">");
                colladaStream.Write(interpString);
                colladaStream.WriteLine("</Name_array>");
                colladaStream.WriteLine(indent + "\t\t\t<technique_common>");
                colladaStream.WriteLine(indent + "\t\t\t\t<accessor source=\"#" + name + "_" + k.ToString() + "InterpArray\" count=\"" + anim.frames.Count + "\" stride=\"1\">");
                colladaStream.WriteLine(indent + "\t\t\t\t\t<param name=\"INTERPOLATION\" type=\"Name\"/>");
                colladaStream.WriteLine(indent + "\t\t\t\t</accessor>");
                colladaStream.WriteLine(indent + "\t\t\t</technique_common>");
                colladaStream.WriteLine(indent + "\t\t</source>");
                colladaStream.WriteLine(indent + "\t\t<sampler id=\"" + name + "_" + k.ToString() + "Sampler\">");
                colladaStream.WriteLine(indent + "\t\t\t<input semantic=\"INPUT\" source=\"#" + name + "_" + k.ToString() + "Input\"/>");
                colladaStream.WriteLine(indent + "\t\t\t<input semantic=\"OUTPUT\" source=\"#" + name + "_" + k.ToString() + "Output\"/>");
                colladaStream.WriteLine(indent + "\t\t\t<input semantic=\"INTERPOLATION\" source=\"#" + name + "_" + k.ToString() + "Interp\"/>");
                colladaStream.WriteLine(indent + "\t\t</sampler>");
                colladaStream.WriteLine(indent + "\t\t<channel source=\"" + name + "_" + k.ToString() + "Sampler\" target=\"Skel" + k.ToString() + "/transform\"/>");
                colladaStream.WriteLine(indent + "\t</animation>");
            }
            colladaStream.WriteLine(indent + "</animation>");
        }

        private void WriteAnimationSequential(StreamWriter colladaStream, List<Animation> anims, int boneCount, string name, MobyModel model, string indent = "")
        {
            colladaStream.WriteLine(indent + "<animation id=\"" + name + "\" name=\"" + name + "\">");

            int frameCount = 0;
            foreach (Animation anim in anims)
            {
                frameCount += anim.frames.Count;
            }

            string timeString = "";
            float frameStartTime = 0.0f;
            foreach (Animation anim in anims)
            {
                foreach (Frame frame in anim.frames)
                {
                    timeString += (frameStartTime).ToString("G", en_US) + " ";
                    if (frame.speed == 0.0f)
                    {
                        frameStartTime += 1.0f / (60.0f * 0.2f);
                    }
                    else
                    {
                        frameStartTime += 1.0f / (60.0f * frame.speed);
                    }
                }
            }

            for (int k = 0; k < boneCount; k++)
            {
                colladaStream.WriteLine(indent + "\t<animation id=\"" + name + "_" + k.ToString() + "\" name=\"" + name + "_" + k.ToString() + "\">");
                colladaStream.WriteLine(indent + "\t\t<source id=\"" + name + "_" + k.ToString() + "Input\">");
                colladaStream.Write(indent + "\t\t\t<float_array id=\"" + name + "_" + k.ToString() + "InputArray\" count=\"" + frameCount + "\">");
                colladaStream.Write(timeString);
                colladaStream.WriteLine(indent + "</float_array>");
                colladaStream.WriteLine(indent + "\t\t\t<technique_common>");
                colladaStream.WriteLine(indent + "\t\t\t\t<accessor source=\"#" + name + "_" + k.ToString() + "InputArray\" count=\"" + frameCount + "\" stride=\"1\">");
                colladaStream.WriteLine(indent + "\t\t\t\t\t<param name=\"TIME\" type=\"float\"/>");
                colladaStream.WriteLine(indent + "\t\t\t\t</accessor>");
                colladaStream.WriteLine(indent + "\t\t\t</technique_common>");
                colladaStream.WriteLine(indent + "\t\t</source>");
                colladaStream.WriteLine(indent + "\t\t<source id=\"" + name + "_" + k.ToString() + "Output\">");
                colladaStream.Write(indent + "\t\t\t<float_array id=\"" + name + "_" + k.ToString() + "OutputArray\" count=\"" + 16 * frameCount + "\">");
                foreach (Animation anim in anims)
                {
                    foreach (Frame frame in anim.frames)
                    {
                        Matrix4 animationMatrix = frame.GetInverseTransformation(k);

                        Vector3 offBone = new Vector3(model.boneDatas[k].translationX, model.boneDatas[k].translationY, model.boneDatas[k].translationZ);

                        offBone *= model.size / 1024f;

                        animationMatrix.M14 += offBone.X;
                        animationMatrix.M24 += offBone.Y;
                        animationMatrix.M34 += offBone.Z;

                        colladaStream.Write((animationMatrix.M11).ToString("G", en_US) + " ");
                        colladaStream.Write((animationMatrix.M12).ToString("G", en_US) + " ");
                        colladaStream.Write((animationMatrix.M13).ToString("G", en_US) + " ");
                        colladaStream.Write((animationMatrix.M14).ToString("G", en_US) + " ");
                        colladaStream.Write((animationMatrix.M21).ToString("G", en_US) + " ");
                        colladaStream.Write((animationMatrix.M22).ToString("G", en_US) + " ");
                        colladaStream.Write((animationMatrix.M23).ToString("G", en_US) + " ");
                        colladaStream.Write((animationMatrix.M24).ToString("G", en_US) + " ");
                        colladaStream.Write((animationMatrix.M31).ToString("G", en_US) + " ");
                        colladaStream.Write((animationMatrix.M32).ToString("G", en_US) + " ");
                        colladaStream.Write((animationMatrix.M33).ToString("G", en_US) + " ");
                        colladaStream.Write((animationMatrix.M34).ToString("G", en_US) + " ");
                        colladaStream.Write((animationMatrix.M41).ToString("G", en_US) + " ");
                        colladaStream.Write((animationMatrix.M42).ToString("G", en_US) + " ");
                        colladaStream.Write((animationMatrix.M43).ToString("G", en_US) + " ");
                        colladaStream.Write((animationMatrix.M44).ToString("G", en_US) + " ");
                    }
                }
                colladaStream.WriteLine("</float_array>");
                colladaStream.WriteLine(indent + "\t\t\t<technique_common>");
                colladaStream.WriteLine(indent + "\t\t\t\t<accessor source=\"#" + name + "_" + k.ToString() + "OutputArray\" count=\"" + frameCount + "\" stride=\"16\">");
                colladaStream.WriteLine(indent + "\t\t\t\t\t<param name=\"TRANSFORM\" type=\"float4x4\"/>");
                colladaStream.WriteLine(indent + "\t\t\t\t</accessor>");
                colladaStream.WriteLine(indent + "\t\t\t</technique_common>");
                colladaStream.WriteLine(indent + "\t\t</source>");
                colladaStream.WriteLine(indent + "\t\t<source id=\"" + name + "_" + k.ToString() + "Interp\">");
                colladaStream.Write(indent + "\t\t\t<Name_array id=\"" + name + "_" + k.ToString() + "InterpArray\" count=\"" + frameCount + "\">");
                for (int j = 0; j < frameCount; j++)
                {
                    colladaStream.Write("LINEAR ");
                }
                colladaStream.WriteLine("</Name_array>");
                colladaStream.WriteLine(indent + "\t\t\t<technique_common>");
                colladaStream.WriteLine(indent + "\t\t\t\t<accessor source=\"#" + name + "_" + k.ToString() + "InterpArray\" count=\"" + frameCount + "\" stride=\"1\">");
                colladaStream.WriteLine(indent + "\t\t\t\t\t<param name=\"INTERPOLATION\" type=\"Name\"/>");
                colladaStream.WriteLine(indent + "\t\t\t\t</accessor>");
                colladaStream.WriteLine(indent + "\t\t\t</technique_common>");
                colladaStream.WriteLine(indent + "\t\t</source>");
                colladaStream.WriteLine(indent + "\t\t<sampler id=\"" + name + "_" + k.ToString() + "Sampler\">");
                colladaStream.WriteLine(indent + "\t\t\t<input semantic=\"INPUT\" source=\"#" + name + "_" + k.ToString() + "Input\"/>");
                colladaStream.WriteLine(indent + "\t\t\t<input semantic=\"OUTPUT\" source=\"#" + name + "_" + k.ToString() + "Output\"/>");
                colladaStream.WriteLine(indent + "\t\t\t<input semantic=\"INTERPOLATION\" source=\"#" + name + "_" + k.ToString() + "Interp\"/>");
                colladaStream.WriteLine(indent + "\t\t</sampler>");
                colladaStream.WriteLine(indent + "\t\t<channel source=\"" + name + "_" + k.ToString() + "Sampler\" target=\"Skel" + k.ToString() + "/transform\"/>");
                colladaStream.WriteLine(indent + "\t</animation>");
            }

            colladaStream.WriteLine(indent + "</animation>");
        }

        private void WriteData(string fileName, Level level, Model model, bool includeSkeleton, int id)
        {
            using (StreamWriter colladaStream = new StreamWriter(fileName))
            {
                // skybox model has no normals and thus the vertex buffer has a different layout
                // if we see other cases like this, it may be advisable to generalize this
                bool skyboxModel = (model is SkyboxModel);
                bool terrainModel = (model is TerrainModel);

                int bufferStride = (skyboxModel) ? 0x06 : 0x08;
                int vOffset = 0x00;
                int vnOffset = 0x03;
                int vtOffset = (skyboxModel) ? 0x03 : 0x06;
                int vcOffset = 0x05;

                int vertexCount = model.vertexBuffer.Length / bufferStride;

                colladaStream.WriteLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
                colladaStream.WriteLine("<COLLADA xmlns=\"http://www.collada.org/2005/11/COLLADASchema\" version=\"1.4.1\">");

                //metadata
                colladaStream.WriteLine("\t<asset>");
                colladaStream.WriteLine("\t\t<contributor>");
                colladaStream.WriteLine("\t\t\t<author>Replanetizer User</author>");
                colladaStream.WriteLine("\t\t\t<authoring_tool>Replanetizer</authoring_tool>");
                colladaStream.WriteLine("\t\t</contributor>");
                colladaStream.WriteLine("\t\t<created>" + DateTime.Now.Year + "-" + DateTime.Now.Month + "-" + DateTime.Now.Day + "T" + DateTime.Now.Hour + ":" + DateTime.Now.Minute + ":" + DateTime.Now.Second + "</created>");
                colladaStream.WriteLine("\t\t<modified>" + DateTime.Now.Year + "-" + DateTime.Now.Month + "-" + DateTime.Now.Day + "T" + DateTime.Now.Hour + ":" + DateTime.Now.Minute + ":" + DateTime.Now.Second + "</modified>");
                colladaStream.WriteLine("\t\t<unit name=\"meter\" meter=\"1\"/>");
                colladaStream.WriteLine("\t\t<up_axis>Z_UP</up_axis>");
                colladaStream.WriteLine("\t</asset>");

                //image
                colladaStream.WriteLine("\t<library_images>");
                foreach (TextureConfig config in model.textureConfig)
                {
                    colladaStream.WriteLine("\t\t<image id=\"texture_" + config.id + "\">");
                    colladaStream.Write("\t\t\t<init_from>");
                    colladaStream.Write(config.id + ".png");
                    colladaStream.WriteLine("</init_from>");
                    colladaStream.WriteLine("\t\t</image>");
                }
                colladaStream.WriteLine("\t</library_images>");

                //effects
                colladaStream.WriteLine("\t<library_effects>");
                foreach (TextureConfig config in model.textureConfig)
                {
                    colladaStream.WriteLine("\t\t<effect id=\"effect_" + config.id + "\">");
                    colladaStream.WriteLine("\t\t\t<profile_COMMON>");
                    colladaStream.WriteLine("\t\t\t\t<newparam sid=\"surface_" + config.id + "\">");
                    colladaStream.WriteLine("\t\t\t\t\t<surface type=\"2D\">");
                    colladaStream.WriteLine("\t\t\t\t\t\t<init_from>texture_" + config.id + "</init_from>");
                    colladaStream.WriteLine("\t\t\t\t\t\t<format>A8R8G8B8</format>");
                    colladaStream.WriteLine("\t\t\t\t\t</surface>");
                    colladaStream.WriteLine("\t\t\t\t</newparam>");
                    colladaStream.WriteLine("\t\t\t\t<newparam sid=\"sampler_" + config.id + "\">");
                    colladaStream.WriteLine("\t\t\t\t\t<sampler2D>");
                    colladaStream.WriteLine("\t\t\t\t\t\t<source>surface_" + config.id + "</source>");
                    colladaStream.WriteLine("\t\t\t\t\t\t<minfilter>LINEAR_MIPMAP_LINEAR</minfilter>");
                    colladaStream.WriteLine("\t\t\t\t\t\t<magfilter>LINEAR</magfilter>");
                    colladaStream.WriteLine("\t\t\t\t\t</sampler2D>");
                    colladaStream.WriteLine("\t\t\t\t</newparam>");
                    colladaStream.WriteLine("\t\t\t\t<technique sid=\"common\">");
                    colladaStream.WriteLine("\t\t\t\t\t<lambert>");
                    colladaStream.WriteLine("\t\t\t\t\t\t<diffuse>");
                    colladaStream.WriteLine("\t\t\t\t\t\t\t<texture texture=\"sampler_" + config.id + "\" texcoord=\"texcoord_" + config.id + "\"/>");
                    colladaStream.WriteLine("\t\t\t\t\t\t</diffuse>");
                    colladaStream.WriteLine("\t\t\t\t\t</lambert>");
                    colladaStream.WriteLine("\t\t\t\t</technique>");
                    colladaStream.WriteLine("\t\t\t</profile_COMMON>");
                    colladaStream.WriteLine("\t\t</effect>");
                }
                colladaStream.WriteLine("\t</library_effects>");

                //materials
                colladaStream.WriteLine("\t<library_materials>");
                foreach (TextureConfig config in model.textureConfig)
                {
                    colladaStream.WriteLine("\t\t<material id=\"material_" + config.id + "\">");
                    colladaStream.WriteLine("\t\t\t<instance_effect url=\"#effect_" + config.id + "\"/>");
                    colladaStream.WriteLine("\t\t</material>");
                }
                colladaStream.WriteLine("\t</library_materials>");

                //geometry
                colladaStream.WriteLine("\t<library_geometries>");
                colladaStream.WriteLine("\t\t<geometry id=\"Model\">");
                colladaStream.WriteLine("\t\t\t<mesh>");
                colladaStream.WriteLine("\t\t\t\t<source id=\"Model_positions\">");
                colladaStream.Write("\t\t\t\t\t<float_array id=\"Model_positions_array\" count=\"" + 3 * vertexCount + "\"> ");
                Vector3[] vertices = new Vector3[vertexCount];
                for (int x = 0; x < vertexCount; x++)
                {
                    float px = model.vertexBuffer[(x * bufferStride) + vOffset + 0x0] * model.size;
                    float py = model.vertexBuffer[(x * bufferStride) + vOffset + 0x1] * model.size;
                    float pz = model.vertexBuffer[(x * bufferStride) + vOffset + 0x2] * model.size;
                    vertices[x] = new Vector3(px, py, pz);
                    colladaStream.Write(px.ToString("G", en_US) + " " + py.ToString("G", en_US) + " " + pz.ToString("G", en_US) + " ");
                }
                colladaStream.WriteLine("</float_array>");
                colladaStream.WriteLine("\t\t\t\t\t<technique_common>");
                colladaStream.WriteLine("\t\t\t\t\t\t<accessor count=\"" + vertexCount + "\" offset=\"0\" source=\"#Model_positions_array\" stride=\"3\">");
                colladaStream.WriteLine("\t\t\t\t\t\t\t<param name=\"X\" type=\"float\"/>");
                colladaStream.WriteLine("\t\t\t\t\t\t\t<param name=\"Y\" type=\"float\"/>");
                colladaStream.WriteLine("\t\t\t\t\t\t\t<param name=\"Z\" type=\"float\"/>");
                colladaStream.WriteLine("\t\t\t\t\t\t</accessor>");
                colladaStream.WriteLine("\t\t\t\t\t</technique_common>");
                colladaStream.WriteLine("\t\t\t\t</source>");
                Vector3[]? normals = (skyboxModel) ? null : new Vector3[vertexCount];
                if (normals != null)
                {
                    colladaStream.WriteLine("\t\t\t\t<source id=\"Model_normals\">");
                    colladaStream.Write("\t\t\t\t\t<float_array id=\"Model_normals_array\" count=\"" + 3 * vertexCount + "\"> ");
                    for (int x = 0; x < vertexCount; x++)
                    {
                        float nx = model.vertexBuffer[(x * bufferStride) + vnOffset + 0x00];
                        float ny = model.vertexBuffer[(x * bufferStride) + vnOffset + 0x01];
                        float nz = model.vertexBuffer[(x * bufferStride) + vnOffset + 0x02];
                        normals[x] = new Vector3(nx, ny, nz);
                        colladaStream.Write(nx.ToString("G", en_US) + " " + ny.ToString("G", en_US) + " " + nz.ToString("G", en_US) + " ");
                    }
                    colladaStream.WriteLine("</float_array>");
                    colladaStream.WriteLine("\t\t\t\t\t<technique_common>");
                    colladaStream.WriteLine("\t\t\t\t\t\t<accessor count=\"" + vertexCount + "\" offset=\"0\" source=\"#Model_normals_array\" stride=\"3\">");
                    colladaStream.WriteLine("\t\t\t\t\t\t\t<param name=\"X\" type=\"float\"/>");
                    colladaStream.WriteLine("\t\t\t\t\t\t\t<param name=\"Y\" type=\"float\"/>");
                    colladaStream.WriteLine("\t\t\t\t\t\t\t<param name=\"Z\" type=\"float\"/>");
                    colladaStream.WriteLine("\t\t\t\t\t\t</accessor>");
                    colladaStream.WriteLine("\t\t\t\t\t</technique_common>");
                    colladaStream.WriteLine("\t\t\t\t</source>");
                }
                if (skyboxModel || terrainModel)
                {
                    colladaStream.WriteLine("\t\t\t\t<source id=\"Model_vertex_colors\">");
                    colladaStream.Write("\t\t\t\t\t<float_array id=\"Model_vertex_colors_array\" count=\"" + 4 * vertexCount + "\"> ");
                    if (skyboxModel)
                    {
                        for (int x = 0; x < vertexCount; x++)
                        {
                            byte[] colors = BitConverter.GetBytes(model.vertexBuffer[(x * bufferStride) + vcOffset + 0x00]);
                            float a = ((float) colors[0]) / 255.0f;
                            float b = ((float) colors[1]) / 255.0f;
                            float g = ((float) colors[2]) / 255.0f;
                            float r = ((float) colors[3]) / 255.0f;
                            colladaStream.Write(r.ToString("G", en_US) + " " + g.ToString("G", en_US) + " " + b.ToString("G", en_US) + " " + a.ToString("G", en_US) + " ");
                        }
                    }
                    else if (terrainModel)
                    {
                        TerrainModel tmodel = (TerrainModel) model;
                        for (int x = 0; x < vertexCount; x++)
                        {
                            float a = ((float) tmodel.rgbas[x * 0x04 + 0x00]) / 255.0f;
                            float b = ((float) tmodel.rgbas[x * 0x04 + 0x01]) / 255.0f;
                            float g = ((float) tmodel.rgbas[x * 0x04 + 0x02]) / 255.0f;
                            float r = ((float) tmodel.rgbas[x * 0x04 + 0x03]) / 255.0f;
                            colladaStream.Write(r.ToString("G", en_US) + " " + g.ToString("G", en_US) + " " + b.ToString("G", en_US) + " " + a.ToString("G", en_US) + " ");
                        }
                    }
                    colladaStream.WriteLine("</float_array>");
                    colladaStream.WriteLine("\t\t\t\t\t<technique_common>");
                    colladaStream.WriteLine("\t\t\t\t\t\t<accessor count=\"" + vertexCount + "\" offset=\"0\" source=\"#Model_vertex_colors_array\" stride=\"4\">");
                    colladaStream.WriteLine("\t\t\t\t\t\t\t<param name=\"R\" type=\"float\"/>");
                    colladaStream.WriteLine("\t\t\t\t\t\t\t<param name=\"G\" type=\"float\"/>");
                    colladaStream.WriteLine("\t\t\t\t\t\t\t<param name=\"B\" type=\"float\"/>");
                    colladaStream.WriteLine("\t\t\t\t\t\t\t<param name=\"A\" type=\"float\"/>");
                    colladaStream.WriteLine("\t\t\t\t\t\t</accessor>");
                    colladaStream.WriteLine("\t\t\t\t\t</technique_common>");
                    colladaStream.WriteLine("\t\t\t\t</source>");
                }
                colladaStream.WriteLine("\t\t\t\t<source id=\"Model_uvs\">");
                colladaStream.Write("\t\t\t\t\t<float_array id=\"Model_uvs_array\" count=\"" + 2 * vertexCount + "\"> ");
                for (int x = 0; x < vertexCount; x++)
                {
                    float tu = model.vertexBuffer[(x * bufferStride) + vtOffset + 0x00];
                    float tv = 1.0f - model.vertexBuffer[(x * bufferStride) + vtOffset + 0x01];
                    colladaStream.Write(tu.ToString("G", en_US) + " " + tv.ToString("G", en_US) + " ");
                }
                colladaStream.WriteLine("</float_array>");
                colladaStream.WriteLine("\t\t\t\t\t<technique_common>");
                colladaStream.WriteLine("\t\t\t\t\t\t<accessor count=\"" + vertexCount + "\" offset=\"0\" source=\"#Model_uvs_array\" stride=\"2\">");
                colladaStream.WriteLine("\t\t\t\t\t\t\t<param name=\"S\" type=\"float\"/>");
                colladaStream.WriteLine("\t\t\t\t\t\t\t<param name=\"T\" type=\"float\"/>");
                colladaStream.WriteLine("\t\t\t\t\t\t</accessor>");
                colladaStream.WriteLine("\t\t\t\t\t</technique_common>");
                colladaStream.WriteLine("\t\t\t\t</source>");
                colladaStream.WriteLine("\t\t\t\t<vertices id=\"Model_vertices\">");
                colladaStream.WriteLine("\t\t\t\t\t<input semantic=\"POSITION\" source=\"#Model_positions\"/>");
                colladaStream.WriteLine("\t\t\t\t</vertices>");
                foreach (TextureConfig config in model.textureConfig)
                {
                    colladaStream.WriteLine("\t\t\t\t<triangles count=\"" + config.size / 3 + "\" material=\"material_symbol_" + config.id + "\">");
                    colladaStream.WriteLine("\t\t\t\t\t<input semantic=\"VERTEX\" source=\"#Model_vertices\" offset=\"0\"/>");
                    colladaStream.WriteLine("\t\t\t\t\t<input semantic=\"NORMAL\" source=\"#Model_normals\" offset=\"0\"/>");
                    colladaStream.WriteLine("\t\t\t\t\t<input semantic=\"TEXCOORD\" source=\"#Model_uvs\" offset=\"0\" set=\"0\"/>");
                    if (skyboxModel || terrainModel)
                        colladaStream.WriteLine("\t\t\t\t\t<input semantic=\"COLOR\" source=\"#Model_vertex_colors\" offset=\"0\"/>");
                    colladaStream.Write("\t\t\t\t\t<p> ");
                    for (int i = config.start / 3; i < config.start / 3 + config.size / 3; i++)
                    {
                        int f1 = model.indexBuffer[i * 3 + 0];
                        int f2 = model.indexBuffer[i * 3 + 1];
                        int f3 = model.indexBuffer[i * 3 + 2];

                        if (ShouldReverseWinding(vertices, normals, f1, f2, f3))
                            (f2, f3) = (f3, f2);

                        colladaStream.Write(f1 + " " + f2 + " " + f3 + " ");
                    }
                    colladaStream.WriteLine("</p>");
                    colladaStream.WriteLine("\t\t\t\t</triangles>");
                }
                colladaStream.WriteLine("\t\t\t</mesh>");
                colladaStream.WriteLine("\t\t</geometry>");
                colladaStream.WriteLine("\t</library_geometries>");

                if (includeSkeleton)
                {
                    MobyModel moby = (MobyModel) model;

                    //controllers
                    colladaStream.WriteLine("\t<library_controllers>");
                    colladaStream.WriteLine("\t\t<controller id=\"Armature\" name=\"Armature\">");
                    colladaStream.WriteLine("\t\t\t<skin source=\"#Model\">");
                    colladaStream.WriteLine("\t\t\t\t<bind_shape_matrix>1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</bind_shape_matrix>");
                    colladaStream.WriteLine("\t\t\t\t<source id=\"Joints\">");
                    colladaStream.Write("\t\t\t\t\t<Name_array id=\"JointsArray\" count=\"" + moby.boneDatas.Count + "\">");
                    for (int i = 0; i < moby.boneCount; i++)
                    {
                        colladaStream.Write("J" + moby.boneMatrices[i].id.ToString() + " ");
                    }
                    colladaStream.WriteLine("\t\t\t\t\t</Name_array>");
                    colladaStream.WriteLine("\t\t\t\t\t<technique_common>");
                    colladaStream.WriteLine("\t\t\t\t\t\t<accessor source=\"#JointsArray\" count=\"" + moby.boneDatas.Count + "\" stride=\"1\">");
                    colladaStream.WriteLine("\t\t\t\t\t\t\t<param name=\"JOINT\" type=\"Name\"/>");
                    colladaStream.WriteLine("\t\t\t\t\t\t</accessor>");
                    colladaStream.WriteLine("\t\t\t\t\t</technique_common>");
                    colladaStream.WriteLine("\t\t\t\t</source>");
                    colladaStream.WriteLine("\t\t\t\t<source id=\"Weights\">");
                    colladaStream.Write("\t\t\t\t\t<float_array id=\"WeightsArray\" count=\"" + vertexCount * 4 + "\">");
                    for (int i = 0; i < vertexCount; i++)
                    {
                        byte[] vWeights = BitConverter.GetBytes(model.weights[i]);
                        for (int j = 0; j < 4; j++)
                        {
                            if (vWeights[j] != 0)
                            {
                                float f = vWeights[j] / 255.0f;
                                colladaStream.Write(f.ToString("G", en_US) + " ");
                            }
                        }
                    }
                    colladaStream.WriteLine("</float_array>");
                    colladaStream.WriteLine("\t\t\t\t\t<technique_common>");
                    colladaStream.WriteLine("\t\t\t\t\t\t<accessor source=\"#WeightsArray\" count=\"" + 4 * vertexCount + "\" stride=\"1\">");
                    colladaStream.WriteLine("\t\t\t\t\t\t\t<param name=\"WEIGHT\" type=\"float\"/>");
                    colladaStream.WriteLine("\t\t\t\t\t\t</accessor>");
                    colladaStream.WriteLine("\t\t\t\t\t</technique_common>");
                    colladaStream.WriteLine("\t\t\t\t</source>");
                    colladaStream.WriteLine("\t\t\t\t<source id=\"InvBindMats\">");
                    colladaStream.Write("\t\t\t\t\t<float_array id=\"InvBindMatsArray\" count=\"" + 16 * moby.boneMatrices.Count + "\">");

                    List<Vector3> offsets = new List<Vector3>();

                    for (int i = 0; i < moby.boneMatrices.Count; i++)
                    {
                        BoneMatrix bmatrix = moby.boneMatrices[i];

                        Vector3 off = new Vector3(bmatrix.cumulativeOffsetX, bmatrix.cumulativeOffsetY, bmatrix.cumulativeOffsetZ);

                        off *= model.size / 1024f;

                        int parent = bmatrix.parent;

                        if (parent != i)
                        {
                            Vector3 poff = offsets[parent];

                            if ((off - poff).LengthSquared < BLENDER_BONE_MIN_LENGTH)
                            {
                                off = poff;
                                off.Z += BLENDER_BONE_FIX;
                            }
                        }

                        offsets.Add(off);
                    }

                    for (int i = 0; i < moby.boneMatrices.Count; i++)
                    {
                        Vector3 off = offsets[i];

                        Matrix3x4 origTrans = moby.boneMatrices[i].transformation;
                        Matrix3 mat = new Matrix3(origTrans.Row0.Xyz, origTrans.Row1.Xyz, origTrans.Row2.Xyz);
                        mat.Transpose();

                        colladaStream.Write((mat.M11).ToString("G", en_US) + " ");
                        colladaStream.Write((mat.M12).ToString("G", en_US) + " ");
                        colladaStream.Write((mat.M13).ToString("G", en_US) + " ");
                        colladaStream.Write((off.X).ToString("G", en_US) + " ");
                        colladaStream.Write((mat.M21).ToString("G", en_US) + " ");
                        colladaStream.Write((mat.M22).ToString("G", en_US) + " ");
                        colladaStream.Write((mat.M23).ToString("G", en_US) + " ");
                        colladaStream.Write((off.Y).ToString("G", en_US) + " ");
                        colladaStream.Write((mat.M31).ToString("G", en_US) + " ");
                        colladaStream.Write((mat.M32).ToString("G", en_US) + " ");
                        colladaStream.Write((mat.M33).ToString("G", en_US) + " ");
                        colladaStream.Write((off.Z).ToString("G", en_US) + " ");
                        colladaStream.Write("0 ");
                        colladaStream.Write("0 ");
                        colladaStream.Write("0 ");
                        colladaStream.Write("1 ");
                    }
                    colladaStream.WriteLine("</float_array>");
                    colladaStream.WriteLine("\t\t\t\t\t<technique_common>");
                    colladaStream.WriteLine("\t\t\t\t\t\t<accessor source=\"#InvBindMatsArray\" count=\"" + moby.boneMatrices.Count + "\" stride=\"16\">");
                    colladaStream.WriteLine("\t\t\t\t\t\t\t<param name=\"TRANSFORM\" type=\"float4x4\"/>");
                    colladaStream.WriteLine("\t\t\t\t\t\t</accessor>");
                    colladaStream.WriteLine("\t\t\t\t\t</technique_common>");
                    colladaStream.WriteLine("\t\t\t\t</source>");
                    colladaStream.WriteLine("\t\t\t\t<joints>");
                    colladaStream.WriteLine("\t\t\t\t\t<input semantic=\"JOINT\" source=\"#Joints\"/>");
                    colladaStream.WriteLine("\t\t\t\t\t<input semantic=\"INV_BIND_MATRIX\" source=\"#InvBindMats\"/>");
                    colladaStream.WriteLine("\t\t\t\t</joints>");
                    colladaStream.WriteLine("\t\t\t\t<vertex_weights count=\"" + vertexCount + "\">");
                    colladaStream.WriteLine("\t\t\t\t\t<input semantic=\"JOINT\" source=\"#Joints\" offset=\"0\"/>");
                    colladaStream.WriteLine("\t\t\t\t\t<input semantic=\"WEIGHT\" source=\"#Weights\" offset=\"1\"/>");
                    colladaStream.Write("\t\t\t\t\t<vcount>");
                    for (int i = 0; i < vertexCount; i++)
                    {
                        byte[] vWeights = BitConverter.GetBytes(model.weights[i]);

                        int c = 0;
                        for (int j = 0; j < 4; j++)
                        {
                            c += (vWeights[j] != 0) ? 1 : 0;
                        }
                        colladaStream.Write(c + " ");
                    }
                    colladaStream.WriteLine("</vcount>");
                    colladaStream.Write("\t\t\t\t\t<v>");
                    int vWeightOffset = 0;
                    for (int i = 0; i < vertexCount; i++)
                    {
                        Byte[] indices = BitConverter.GetBytes(model.ids[i]);
                        byte[] vWeights = BitConverter.GetBytes(model.weights[i]);

                        for (int j = 0; j < 4; j++)
                        {
                            if (vWeights[j] != 0)
                            {
                                colladaStream.Write(indices[j] + " " + vWeightOffset + " ");
                                vWeightOffset++;
                            }
                        }
                    }
                    colladaStream.WriteLine("</v>");
                    colladaStream.WriteLine("\t\t\t\t</vertex_weights>");
                    colladaStream.WriteLine("\t\t\t</skin>");
                    colladaStream.WriteLine("\t\t</controller>");
                    colladaStream.WriteLine("\t</library_controllers>");

                    if (settings.animationChoice != ExporterModelSettings.AnimationChoice.None)
                    {
                        //animations
                        List<Animation> anims;

                        if (moby.id == 0)
                        {
                            anims = level.playerAnimations;
                        }
                        else
                        {
                            anims = moby.animations;
                        }

                        colladaStream.WriteLine("\t<library_animations>");
                        if (id == -1)
                        {
                            if (settings.animationChoice == ExporterModelSettings.AnimationChoice.AllSequential)
                            {
                                WriteAnimationSequential(colladaStream, anims, moby.boneCount, "Anim", moby, "\t\t");
                            }
                            else
                            {
                                for (int i = 0; i < anims.Count; i++)
                                {
                                    WriteAnimation(colladaStream, anims[i], moby.boneCount, "Anim" + i.ToString(), "\t\t");
                                }
                            }
                        }
                        else
                        {
                            WriteAnimation(colladaStream, anims[id], moby.boneCount, "Anim" + id.ToString(), "\t\t");
                        }
                        colladaStream.WriteLine("\t</library_animations>");
                    }
                }

                //scene
                colladaStream.WriteLine("\t<library_visual_scenes>");
                colladaStream.WriteLine("\t\t<visual_scene id=\"Scene\" name=\"Scene\">");
                if (includeSkeleton)
                {
                    MobyModel moby = (MobyModel) model;

                    if (moby.skeleton != null)
                        WriteSkeleton(colladaStream, moby.skeleton, model.size, "\t\t\t");
                }
                colladaStream.WriteLine("\t\t\t<node id=\"Object\" name=\"Object\" type=\"NODE\">");
                colladaStream.WriteLine("\t\t\t\t<matrix sid=\"transform\">1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1</matrix>");
                if (includeSkeleton)
                {
                    colladaStream.WriteLine("\t\t\t\t<instance_controller url=\"#Armature\" name=\"Armature\">");
                    colladaStream.WriteLine("\t\t\t\t\t<skeleton>#Skel0</skeleton>");
                }
                else
                {
                    colladaStream.WriteLine("\t\t\t\t<instance_geometry url=\"#Model\" name=\"Model\">");
                }
                colladaStream.WriteLine("\t\t\t\t\t<bind_material>");
                colladaStream.WriteLine("\t\t\t\t\t\t<technique_common>");
                foreach (TextureConfig config in model.textureConfig)
                {
                    colladaStream.WriteLine("\t\t\t\t\t\t\t<instance_material symbol=\"material_symbol_" + config.id + "\" target=\"#material_" + config.id + "\">");
                    colladaStream.WriteLine("\t\t\t\t\t\t\t\t<bind_vertex_input semantic=\"texcoord_" + config.id + "\" input_semantic=\"TEXCOORD\" input_set=\"0\"/>");
                    colladaStream.WriteLine("\t\t\t\t\t\t\t</instance_material>");
                }
                colladaStream.WriteLine("\t\t\t\t\t\t</technique_common>");
                colladaStream.WriteLine("\t\t\t\t\t</bind_material>");
                if (includeSkeleton)
                {
                    colladaStream.WriteLine("\t\t\t\t</instance_controller>");
                }
                else
                {
                    colladaStream.WriteLine("\t\t\t\t</instance_geometry>");
                }
                colladaStream.WriteLine("\t\t\t</node>");
                colladaStream.WriteLine("\t\t</visual_scene>");
                colladaStream.WriteLine("\t</library_visual_scenes>");
                colladaStream.WriteLine("\t<scene>");
                colladaStream.WriteLine("\t\t<instance_visual_scene url=\"#Scene\"/>");
                colladaStream.WriteLine("\t</scene>");

                colladaStream.WriteLine("</COLLADA>");
            }
        }

        public override void ExportModel(string fileName, Level level, Model model)
        {
            LOGGER.Trace(fileName);

            bool includeSkeleton = (model is MobyModel mobyModel && mobyModel.boneCount != 0 && level.game.num != 4);

            if (includeSkeleton && (settings.animationChoice == ExporterModelSettings.AnimationChoice.AllSeparate))
            {
                string fileExt = Path.GetExtension(fileName);
                string? dir = Path.GetDirectoryName(fileName);
                fileName = Path.GetFileNameWithoutExtension(fileName);

                if (dir == null) dir = "";

                int numFilesExported = ((MobyModel) model).animations.Count;

                for (int i = 0; i < numFilesExported; i++)
                {
                    string filePath = Path.Combine(dir, fileName + "_" + i.ToString() + fileExt);

                    WriteData(filePath, level, model, true, i);
                }
            }
            else
            {
                WriteData(fileName, level, model, includeSkeleton, -1);
            }
        }
    }
}
