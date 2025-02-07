﻿// Copyright (C) 2018-2021, The Replanetizer Contributors.
// Replanetizer is free software: you can redistribute it
// and/or modify it under the terms of the GNU General Public
// License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// Please see the LICENSE.md file for more details.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Drawing;
using ImGuiNET;
using LibReplanetizer.LevelObjects;
using LibReplanetizer;
using Replanetizer.Utils;

namespace Replanetizer.Frames
{
    public class PropertyFrame : Frame
    {
        protected sealed override string frameName { get; set; } = "Properties";

        private Selection? _selection;

        public Selection? selection
        {
            get => _selection;
            set
            {
                if (_selection != null)
                    _selection.CollectionChanged -= SelectionOnCollectionChanged;
                if (value != null)
                    value.CollectionChanged += SelectionOnCollectionChanged;
                _selection = value;
                UpdateFromSelection();
            }
        }

        private object? _selectedObject;

        public object? selectedObject
        {
            get => _selectedObject;
            set
            {
                if (!listenToCallbacks)
                    return;
                _selectedObject = value;
                RecomputeProperties();
            }
        }

        private bool listenToCallbacks;
        private bool hideCallbackButton;
        private LevelFrame? levelFrame;

        private Dictionary<string, Dictionary<string, PropertyInfo>> properties = new();

        public PropertyFrame(
            Window wnd, LevelFrame? levelFrame = null, string? overrideFrameName = null,
            bool listenToCallbacks = false, bool hideCallbackButton = false) : base(wnd)
        {
            if (overrideFrameName is { Length: > 0 })
                frameName = overrideFrameName;

            this.levelFrame = levelFrame;
            this.listenToCallbacks = listenToCallbacks;
            this.hideCallbackButton = hideCallbackButton;
        }

        private void UpdateLevelFrame()
        {
            levelFrame?.InvalidateView();
        }

        private void SelectionOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateFromSelection();
        }

        private void UpdateFromSelection()
        {
            if (selection == null)
                // This shouldn't happen
                return;
            selection.TryGetOne(out var obj);
            selectedObject = obj;
        }

        private void RecomputeProperties()
        {
            properties.Clear();

            if (selectedObject == null)
                return;

            var objProps = selectedObject.GetType().GetProperties();
            foreach (var prop in objProps)
            {
                string category =
                    prop.GetCustomAttribute<CategoryAttribute>()?.Category ?? "Unknowns";

                string displayName =
                    prop.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? prop.Name;

                if (!properties.ContainsKey(category))
                    properties[category] = new Dictionary<string, PropertyInfo>();

                properties[category][displayName] = prop;
            }
        }

        public override void RenderAsWindow(float deltaTime)
        {
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(550, 0));
            if (ImGui.Begin(frameName, ref isOpen))
            {
                Render(deltaTime);
                ImGui.End();
            }
        }

        public override void Render(float deltaTime)
        {
            if (selectedObject == null)
            {
                if (selection is { Count: > 1 })
                    ImGui.Text("Multiple objects selected");
                else
                    ImGui.Text("Select an object");
                return;
            }

            if (!hideCallbackButton && listenToCallbacks)
            {
                if (ImGui.Button("Stop following object selection"))
                {
                    listenToCallbacks = false;
                }
            }

            foreach (var (categoryName, categoryItems) in properties)
            {
                RenderCategory(categoryName, categoryItems);
            }
        }

        private void RenderCategory(string categoryName, Dictionary<string, PropertyInfo> categoryItems)
        {
            if (ImGui.CollapsingHeader(categoryName, ImGuiTreeNodeFlags.DefaultOpen))
            {
                foreach (var (key, value) in categoryItems)
                    RenderCategoryItem(key, value);
            }
        }

        private void RenderCategoryItem(string propertyName, PropertyInfo propertyInfo)
        {
            object? val = propertyInfo.GetValue(selectedObject);
            Type? type = propertyInfo.GetSetMethod() == null ? null : propertyInfo.PropertyType;

            if (val == null)
            {
                ImGui.LabelText(propertyName, "null");
            }
            else if (type == typeof(string))
            {
                byte[] v = Encoding.ASCII.GetBytes(val as string ?? string.Empty);
                if (ImGui.InputText(propertyName, v, (uint) v.Length))
                {
                    propertyInfo.SetValue(selectedObject, Encoding.ASCII.GetString(v));
                    UpdateLevelFrame();
                }
            }
            else if (type == typeof(int))
            {
                int v = (int) val;
                if (ImGui.InputInt(propertyName, ref v))
                {
                    propertyInfo.SetValue(selectedObject, v);
                    if (selectedObject is ModelObject modelObject && levelFrame != null)
                    {
                        modelObject.TryChangeModel(levelFrame.level);
                    }
                    UpdateLevelFrame();
                }
            }
            else if (type == typeof(uint))
            {
                int v = unchecked((int) (uint) val);
                if (ImGui.InputInt(propertyName, ref v))
                {
                    propertyInfo.SetValue(selectedObject, unchecked((uint) v));
                    UpdateLevelFrame();
                }
            }
            else if (type == typeof(short))
            {
                int v = Convert.ToInt32(val);
                if (ImGui.InputInt(propertyName, ref v))
                {
                    propertyInfo.SetValue(selectedObject, (short) (v & 0xffff));
                    UpdateLevelFrame();
                }
            }
            else if (type == typeof(ushort))
            {
                int v = (ushort) val;
                if (ImGui.InputInt(propertyName, ref v))
                {
                    propertyInfo.SetValue(selectedObject, unchecked((ushort) (v & 0xffff)));
                    UpdateLevelFrame();
                }
            }
            else if (type == typeof(float))
            {
                float v = (float) val;
                if (ImGui.InputFloat(propertyName, ref v))
                {
                    propertyInfo.SetValue(selectedObject, v);
                    UpdateLevelFrame();
                }
            }
            else if (type == typeof(Color))
            {
                var c = (Color) val;
                var v = new Vector3(c.R / 255.0f, c.G / 255.0f, c.B / 255.0f);
                if (ImGui.ColorEdit3(propertyName, ref v))
                {
                    Color newColor = Color.FromArgb(
                        (int) (v.X * 255.0f), (int) (v.Y * 255.0f), (int) (v.Z * 255.0f)
                    );
                    propertyInfo.SetValue(selectedObject, newColor);
                    UpdateLevelFrame();
                }
            }
            else if (type == typeof(OpenTK.Mathematics.Vector3))
            {
                var origV = (OpenTK.Mathematics.Vector3) val;
                var v = new Vector3(origV.X, origV.Y, origV.Z);
                if (ImGui.InputFloat3(propertyName, ref v))
                {
                    origV.X = v.X;
                    origV.Y = v.Y;
                    origV.Z = v.Z;
                    propertyInfo.SetValue(selectedObject, origV);

                    if (selectedObject is LevelObject levelObject)
                        levelObject.UpdateTransformMatrix();

                    UpdateLevelFrame();
                }
            }
            else if (type == typeof(OpenTK.Mathematics.Quaternion))
            {
                var origRot = ((OpenTK.Mathematics.Quaternion) val).ToEulerAngles();
                var v = new Vector3(origRot.X, origRot.Y, origRot.Z);
                if (ImGui.InputFloat3(propertyName, ref v))
                {
                    origRot.X = v.X;
                    origRot.Y = v.Y;
                    origRot.Z = v.Z;
                    propertyInfo.SetValue(
                        selectedObject,
                        new OpenTK.Mathematics.Quaternion(origRot.X, origRot.Y, origRot.Z)
                    );

                    if (selectedObject is LevelObject levelObject)
                        levelObject.UpdateTransformMatrix();

                    UpdateLevelFrame();
                }
            }
            else if (type == typeof(OpenTK.Mathematics.Matrix4))
            {
                var mat = (OpenTK.Mathematics.Matrix4) val;
                var v1 = new Vector4(mat.M11, mat.M12, mat.M13, mat.M14);
                var v2 = new Vector4(mat.M21, mat.M22, mat.M23, mat.M24);
                var v3 = new Vector4(mat.M31, mat.M32, mat.M33, mat.M34);
                var v4 = new Vector4(mat.M41, mat.M42, mat.M43, mat.M44);

                bool change = false;

                if (ImGui.InputFloat4($"{propertyName} Row 1", ref v1))
                {
                    change = true;
                    mat.M11 = v1.X;
                    mat.M12 = v1.Y;
                    mat.M13 = v1.Z;
                    mat.M14 = v1.W;
                }

                if (ImGui.InputFloat4($"{propertyName} Row 2", ref v2))
                {
                    change = true;
                    mat.M21 = v2.X;
                    mat.M22 = v2.Y;
                    mat.M23 = v2.Z;
                    mat.M24 = v2.W;
                }

                if (ImGui.InputFloat4($"{propertyName} Row 3", ref v3))
                {
                    change = true;
                    mat.M31 = v3.X;
                    mat.M32 = v3.Y;
                    mat.M33 = v3.Z;
                    mat.M34 = v3.W;
                }

                if (ImGui.InputFloat4($"{propertyName} Row 4", ref v4))
                {
                    change = true;
                    mat.M41 = v4.X;
                    mat.M42 = v4.Y;
                    mat.M43 = v4.Z;
                    mat.M44 = v4.W;
                }

                if (change)
                {
                    propertyInfo.SetValue(selectedObject, mat);

                    if (selectedObject is LevelObject levelObject)
                        levelObject.UpdateTransformMatrix();

                    UpdateLevelFrame();
                }
            }
            else if (type is { IsArray: true })
            {
                if (ImGui.CollapsingHeader(propertyName))
                {
                    Array array = (Array) val;

                    foreach (object o in array)
                        ImGui.Text(Convert.ToString(o));
                }
            }
            else if (type is { IsEnum: true })
            {
                Array values = Enum.GetValues(type);
                string[] strings = new string[values.Length];
                for (int i = 0; i < values.Length; i++)
                    strings[i] = Convert.ToString(values.GetValue(i)) ?? string.Empty;

                int index = (int) val;
                if (index < values.Length)
                {
                    if (ImGui.Combo(propertyName, ref index, strings, values.Length))
                    {
                        propertyInfo.SetValue(selectedObject, index);
                        UpdateLevelFrame();
                    }
                }
                else
                    ImGui.LabelText(propertyName, "[Out of Range] " + Convert.ToString(index));
            }
            else if (type is { IsGenericType: true } && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                ICollection list = (ICollection) val;
                Type genericType = type.GetGenericArguments()[0];
                if (genericType == typeof(TextureConfig))
                {
                    if (ImGui.CollapsingHeader(propertyName))
                    {
                        List<TextureConfig> textureConfigs = (List<TextureConfig>) val;

                        PropertyInfo[] texConfProps = typeof(TextureConfig).GetProperties();

                        int i = 1;

                        foreach (TextureConfig t in list)
                        {
                            ImGui.Text("Texture Config " + i);

                            foreach (PropertyInfo prop in texConfProps)
                            {
                                string displayName =
                                    prop.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? prop.Name;

                                object? o = prop.GetValue(t);
                                ImGui.LabelText(displayName, (o == null) ? "Null" : o.ToString());
                            }

                            i++;
                        }
                    }
                }
                else
                {
                    string genericTypeName = type.GetGenericArguments()[0].Name;
                    ImGui.LabelText(propertyName, "List<" + genericTypeName + ">[" + list.Count + "]");
                }
            }
            else
                ImGui.LabelText(propertyName, Convert.ToString(val));
        }
    }
}
