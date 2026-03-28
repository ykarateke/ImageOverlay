// <copyright file="ImageOverlaySystem.cs" company="algernon (K. Algernon A. Sheppard)">
// Copyright (c) algernon (K. Algernon A. Sheppard). All rights reserved.
// Licensed under the Apache Licence, Version 2.0 (the "License"); you may not use this file except in compliance with the License.
// See LICENSE.txt file in the project root for full license information.
// </copyright>

namespace ImageOverlay
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using Colossal.Logging;
    using Colossal.Serialization.Entities;
    using Game;
    using Game.Input;
    using Game.Simulation;
    using Unity.Mathematics;
    using UnityEngine;
    using static ActionNames;

    /// <summary>
    /// The historical start mod system.
    /// </summary>
    internal sealed partial class ImageOverlaySystem : GameSystemBase
    {
        // Terrain mesh resolution: arazi uyum kalitesi (yüksek = daha iyi ama daha ağır).
        private const int TerrainMeshResolution = 64;

        // Input actions.
        private readonly List<KeyValuePair<ProxyAction, Action>> _actions = new ();

        // References.
        private ILog _log;

        // Overlay objects.
        private GameObject _overlayObject;
        private Mesh _overlayMesh;
        private Material _overlayMaterial;
        private Texture2D _overlayTexture;
        private Shader _overlayShader;
        private bool _isVisible = false;

        // Status flag.
        private bool _shaderInitialized = false;

        /// <summary>
        /// Gets the active instance.
        /// </summary>
        internal static ImageOverlaySystem Instance { get; private set; }

        /// <summary>
        /// Triggers a refresh of the current overlay (if any).
        /// </summary>
        internal void UpdateOverlay()
        {
            // Only refresh if there's an existing overlay object.
            if (_overlayObject)
            {
                UpdateOverlayTexture();
            }
        }

        /// <summary>
        /// Sets whether the overlay will be displayed through terrain.
        /// </summary>
        /// <param name="showThroughTerrain"><c>true</c> to have the image still appear through terrain, <c>false</c> to respect terrain opacity.</param>
        internal void ShowThroughTerrain(bool showThroughTerrain)
        {
            if (_overlayObject?.GetComponent<Renderer>()?.material is Material overlayMaterial)
            {
                overlayMaterial.SetFloat("_ZTest", showThroughTerrain ? 8f : 4f);
                _shaderInitialized = true;
            }
            else
            {
                _log.Info("Unable set ZTest: overlay material shader not yet ready.");
            }
        }

        /// <summary>
        /// Sets the overlay's alpha value.
        /// </summary>
        /// <param name="alpha">Alpha value to set (0f - 1f).</param>
        internal void SetAlpha(float alpha)
        {
            if (_overlayObject)
            {
                // Invert alpha.
                _overlayObject.GetComponent<Renderer>().material.color = new Color(1f, 1f, 1f, 1f - alpha);
            }
        }

        /// <summary>
        /// Sets the overlay size.
        /// </summary>
        /// <param name="size">Size per size, in metres.</param>
        internal void SetSize(float size)
        {
            if (_overlayObject)
            {
                GenerateTerrainMesh();
            }
        }

        /// <summary>
        /// Sets the overlay's X-position.
        /// </summary>
        /// <param name="posX">X position, in metres.</param>
        internal void SetPositionX(float posX)
        {
            if (_overlayObject)
            {
                GenerateTerrainMesh();
            }
        }

        /// <summary>
        /// Sets the overlay's elevation.
        /// </summary>
        /// <param name="elevation">Elevation, in metres.</param>
        internal void SetPositionY(float elevation)
        {
            // Yükseklik arazi mesh'ine baked edildiğinden sadece mesh'i yeniden oluştur.
            if (_overlayObject)
            {
                GenerateTerrainMesh();
            }
        }

        /// <summary>
        /// Sets the overlay's Z-position.
        /// </summary>
        /// <param name="posZ">Z position, in metres.</param>
        internal void SetPositionZ(float posZ)
        {
            if (_overlayObject)
            {
                GenerateTerrainMesh();
            }
        }

        /// <summary>
        /// Resets the overlay elevation to 5m above the surface level at the exact centre of the map.
        /// </summary>
        internal void ResetElevation()
        {
            TerrainHeightData terrainHeight = World.GetOrCreateSystemManaged<TerrainSystem>().GetHeightData();
            WaterSurfaceData<SurfaceWater> waterSurface = World.GetOrCreateSystemManaged<WaterSystem>().GetSurfaceData(out _);
            Mod.Instance.ActiveSettings.OverlayPosY = WaterUtils.SampleHeight(ref waterSurface, ref terrainHeight, float3.zero) + 5f;
        }

        /// <summary>
        /// Updates the overlay's rotation to match current settings.
        /// Rotation is baked into mesh UVs — no transform rotation needed.
        /// </summary>
        internal void UpdateRotation()
        {
            if (_overlayObject)
            {
                // Rotasyon UV'lere baked edildiğinden mesh'i yeniden oluştur.
                GenerateTerrainMesh();
            }
        }

        /// <summary>
        /// Called when the system is created.
        /// </summary>
        protected override void OnCreate()
        {
            base.OnCreate();

            // Set instance.
            Instance = this;

            // Set log.
            _log = Mod.Instance.Log;
            _log.Info("OnCreate");

            // Try to load shader.
            if (!LoadShader())
            {
                // Shader loading error; abort operation.
                return;
            }

            // Get input actions from settings.
            ModSettings activeSettings = Mod.Instance.ActiveSettings;

            // Assign input actions.
            _actions.Add(new (activeSettings.GetAction(ToggleAction), ToggleOverlay));
            _actions.Add(new (activeSettings.GetAction(MoveUpAction), () => { activeSettings.OverlayPosY += 1f; }));
            _actions.Add(new (activeSettings.GetAction(MoveDownAction), () => { activeSettings.OverlayPosY -= 1f; }));
            _actions.Add(new (activeSettings.GetAction(MoveUpLargeAction), () => { activeSettings.OverlayPosY += 10f; }));
            _actions.Add(new (activeSettings.GetAction(MoveDownLargeAction), () => { activeSettings.OverlayPosY -= 10f; }));
            _actions.Add(new (activeSettings.GetAction(MoveNorthAction), () => { activeSettings.OverlayPosZ += 1f; }));
            _actions.Add(new (activeSettings.GetAction(MoveSouthAction), () => { activeSettings.OverlayPosZ -= 1f; }));
            _actions.Add(new (activeSettings.GetAction(MoveEastAction), () => { activeSettings.OverlayPosX += 1f; }));
            _actions.Add(new (activeSettings.GetAction(MoveWestAction), () => { activeSettings.OverlayPosX -= 1f; }));
            _actions.Add(new (activeSettings.GetAction(MoveNorthLargeAction), () => { activeSettings.OverlayPosZ += 10f; }));
            _actions.Add(new (activeSettings.GetAction(MoveSouthLargeAction), () => { activeSettings.OverlayPosZ -= 10f; }));
            _actions.Add(new (activeSettings.GetAction(MoveEastLargeAction), () => { activeSettings.OverlayPosX += 10f; }));
            _actions.Add(new (activeSettings.GetAction(MoveWestLargeAction), () => { activeSettings.OverlayPosX -= 10f; }));
            _actions.Add(new (activeSettings.GetAction(RotateLeftAction), () => { activeSettings.OverlayRotation -= 1f; }));
            _actions.Add(new (activeSettings.GetAction(RotateRightAction), () => { activeSettings.OverlayRotation += 1f; }));
            _actions.Add(new (activeSettings.GetAction(RotateLeftLargeAction), () => { activeSettings.OverlayRotation -= 90f; }));
            _actions.Add(new (activeSettings.GetAction(RotateRightLargeAction), () => { activeSettings.OverlayRotation += 90f; }));
            _actions.Add(new (activeSettings.GetAction(IncreaseSizeAction), () => { activeSettings.OverlaySize += 10f; }));
            _actions.Add(new (activeSettings.GetAction(DecreaseSizeAction), () => { activeSettings.OverlaySize -= 10f; }));
            _actions.Add(new (activeSettings.GetAction(IncreaseSizeLargeAction), () => { activeSettings.OverlaySize += 100f; }));
            _actions.Add(new (activeSettings.GetAction(DecreaseSizeLargeAction), () => { activeSettings.OverlaySize -= 100f; }));

            _log.Info("Finished OnCreate");
        }

        /// <summary>
        /// Called when loading is complete.
        /// </summary>
        /// <param name="purpose">Loading purpose.</param>
        /// <param name="mode">Current game mode.</param>
        protected override void OnGameLoadingComplete(Purpose purpose, GameMode mode)
        {
            base.OnGameLoadingComplete(purpose, mode);
            if ((mode & GameMode.GameOrEditor) != GameMode.None)
            {
                foreach (KeyValuePair<ProxyAction, Action> entry in _actions)
                {
                    entry.Key.shouldBeEnabled = true;
                }
            }
            else
            {
                foreach (KeyValuePair<ProxyAction, Action> entry in _actions)
                {
                    entry.Key.shouldBeEnabled = false;
                }
            }
        }

        /// <summary>
        /// Called every update.
        /// </summary>
        protected override void OnUpdate()
        {
            ModSettings activeSettings = Mod.Instance.ActiveSettings;
            bool locked = activeSettings.IsLocked;

            foreach (KeyValuePair<ProxyAction, Action> entry in _actions)
            {
                if (!entry.Key.WasPerformedThisFrame())
                {
                    continue;
                }

                // Kilitli modda toggle (aç/kapat) hariç tüm kısayollar devre dışı.
                if (locked && entry.Key.name != ToggleAction)
                {
                    continue;
                }

                _log.Info($"Performing action {entry.Key.name}");
                entry.Value();
            }
        }

        /// <summary>
        /// Called when the system is destroyed.
        /// </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();

            DestroyObjects();
        }

        /// <summary>
        /// Toggles the overlay (called by hotkey action).
        /// </summary>
        private void ToggleOverlay()
        {
            _log.Info("Toggling overlay");

            // Hide overlay if it's currently visible.
            if (_isVisible)
            {
                _isVisible = false;
                if (_overlayObject)
                {
                    _overlayObject.SetActive(false);
                }

                return;
            }

            // Showing overlay - create overlay if it's not already there, or if the file we used has been deleted.
            if (!_overlayObject || !_overlayMaterial || !_overlayTexture)
            {
                CreateOverlay();
            }

            // Show overlay if one was successfully loaded.
            if (_overlayObject)
            {
                _overlayObject.SetActive(true);
                _isVisible = true;
            }
            else
            {
                _log.Info("Overlay object wasn't created");
            }

            // Ensure initial shader initialization if needed.
            if (!_shaderInitialized)
            {
                ShowThroughTerrain(Mod.Instance.ActiveSettings.ShowThroughTerrain);
            }
        }

        /// <summary>
        /// Updates the overlay texture.
        /// </summary>
        private void UpdateOverlayTexture()
        {
            // Ensure file exists.
            string selectedOverlay = Mod.Instance.ActiveSettings.SelectedOverlay;
            if (string.IsNullOrEmpty(selectedOverlay))
            {
                _log.Info($"no overlay file set");
                return;
            }

            if (!File.Exists(selectedOverlay))
            {
                _log.Info($"invalid overlay file {selectedOverlay}");
                return;
            }

            _log.Info($"loading image file {selectedOverlay}");

            // Ensure texture instance.
            _overlayTexture ??= new Texture2D(1, 1, TextureFormat.ARGB32, false);

            // Load and apply texture.
            _overlayTexture.LoadImage(File.ReadAllBytes(selectedOverlay));
            _overlayTexture.Apply();

            // Create material.
            _overlayMaterial ??= new Material(_overlayShader)
            {
                mainTexture = _overlayTexture,
            };
        }

        /// <summary>
        /// Creates the overlay object.
        /// </summary>
        private void CreateOverlay()
        {
            // Dispose of any existing objects.
            DestroyObjects();

            try
            {
                // Texture ve materyali yükle.
                UpdateOverlayTexture();

                // Boş GameObject oluştur, MeshFilter ve MeshRenderer ekle.
                _overlayObject = new GameObject("ImageOverlay_TerrainMesh");
                _overlayObject.AddComponent<MeshFilter>();
                _overlayObject.AddComponent<MeshRenderer>();

                // Mesh nesnesini oluştur ve MeshFilter'a bağla.
                _overlayMesh = new Mesh();
                _overlayMesh.name = "ImageOverlay_Mesh";
                _overlayMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                _overlayObject.GetComponent<MeshFilter>().mesh = _overlayMesh;

                // Araziye uyan mesh'i oluştur.
                GenerateTerrainMesh();

                // Materyali uygula.
                _overlayObject.GetComponent<Renderer>().material = _overlayMaterial;
                SetAlpha(Mod.Instance.ActiveSettings.Alpha);

                _log.Info("Terrain-conforming overlay mesh oluşturuldu.");
            }
            catch (Exception e)
            {
                _log.Error(e, "exception loading image overlay file");
            }
        }

        /// <summary>
        /// Generates (or regenerates) a terrain-conforming mesh.
        /// Arazi yüksekliklerini örnekler ve görüntüyü yüzeye yapıştırır.
        /// Vertex'ler local space'de üretilir (precision kayması önlenir).
        /// </summary>
        private void GenerateTerrainMesh()
        {
            if (_overlayMesh == null)
            {
                return;
            }

            float size = Mod.Instance.ActiveSettings.OverlaySize;
            float cx = Mod.Instance.ActiveSettings.OverlayPosX;
            float cz = Mod.Instance.ActiveSettings.OverlayPosZ;
            float elevationOffset = Mod.Instance.ActiveSettings.IsLocked ? 0f : Mod.Instance.ActiveSettings.OverlayPosY;

            // GameObject'i overlay merkezine taşı — vertex'ler local space'de kalacak.
            // Büyük dünya koordinatlarının float precision kaybını (kamera kayması) önler.
            _overlayObject.transform.position = new Vector3(cx, 0f, cz);

            int res = TerrainMeshResolution;
            int vCount = res + 1;

            Vector3[] vertices = new Vector3[vCount * vCount];
            Vector2[] uvs = new Vector2[vCount * vCount];
            int[] triangles = new int[res * res * 6];

            TerrainHeightData heightData = World.GetOrCreateSystemManaged<TerrainSystem>().GetHeightData();
            WaterSurfaceData<SurfaceWater> waterSurface = World.GetOrCreateSystemManaged<WaterSystem>().GetSurfaceData(out _);

            float halfSize = size * 0.5f;
            float step = size / res;

            // Rotasyonu UV'lere bake et.
            float rotRad = Mod.Instance.ActiveSettings.OverlayRotation * Mathf.Deg2Rad;
            float cosR = Mathf.Cos(rotRad);
            float sinR = Mathf.Sin(rotRad);

            for (int zi = 0; zi <= res; zi++)
            {
                for (int xi = 0; xi <= res; xi++)
                {
                    int i = (zi * vCount) + xi;

                    // Dünya koordinatı (height sampling için).
                    float wx = cx - halfSize + (xi * step);
                    float wz = cz - halfSize + (zi * step);

                    // Arazi yüksekliği + offset. Local space için merkez çıkarılır.
                    float wy = WaterUtils.SampleHeight(ref waterSurface, ref heightData, new float3(wx, 0f, wz)) + elevationOffset + 0.5f;

                    // LOCAL space vertex (dünya koordinatından merkez çıkarılır — precision fix).
                    vertices[i] = new Vector3(wx - cx, wy, wz - cz);

                    // UV koordinatları: ortalanmış UV + rotasyon.
                    float u = ((float)xi / res) - 0.5f;
                    float v = ((float)zi / res) - 0.5f;
                    uvs[i] = new Vector2((u * cosR) - (v * sinR) + 0.5f, (u * sinR) + (v * cosR) + 0.5f);
                }
            }

            int tri = 0;
            for (int zi = 0; zi < res; zi++)
            {
                for (int xi = 0; xi < res; xi++)
                {
                    int i = (zi * vCount) + xi;
                    triangles[tri++] = i;
                    triangles[tri++] = i + vCount;
                    triangles[tri++] = i + vCount + 1;
                    triangles[tri++] = i;
                    triangles[tri++] = i + vCount + 1;
                    triangles[tri++] = i + 1;
                }
            }

            _overlayMesh.Clear();
            _overlayMesh.vertices = vertices;
            _overlayMesh.uv = uvs;
            _overlayMesh.triangles = triangles;
            _overlayMesh.RecalculateNormals();
            _overlayMesh.RecalculateBounds();
        }

        /// <summary>
        /// Loads the custom shader from file.
        /// </summary>
        /// <returns><c>true</c> if the shader was successfully loaded, <c>false</c> otherwise.</returns>
        private bool LoadShader()
        {
            try
            {
                _log.Info("loading overlay shader");
                using StreamReader reader = new (Assembly.GetExecutingAssembly().GetManifestResourceStream("ImageOverlay.Shader.shaderbundle"));
                {
                    // Extract shader from file.
                    _overlayShader = AssetBundle.LoadFromStream(reader.BaseStream)?.LoadAsset<Shader>("Assets/UnlitTransparentAdditive.shader");
                    if (_overlayShader is not null)
                    {
                        // Shader loaded - all good!
                        return true;
                    }
                    else
                    {
                        _log.Critical("Image Overlay: unable to load overlay shader from asset bundle; aborting operation.");
                    }
                }
            }
            catch (Exception e)
            {
                _log.Critical(e, "Image Overlay: exception loading overlay shader; aborting operation.");
            }

            // If we got here, something went wrong.
            return false;
        }

        /// <summary>
        /// Destroys any existing texture and GameObject.
        /// </summary>
        private void DestroyObjects()
        {
            // Overlay texture.
            if (_overlayTexture)
            {
                _log.Info("destroying existing overlay texture");
                UnityEngine.Object.DestroyImmediate(_overlayTexture);
                _overlayTexture = null;
            }

            // Overlay material.
            if (_overlayMaterial)
            {
                _log.Info("destroying existing overlay material");
                UnityEngine.Object.DestroyImmediate(_overlayMaterial);
                _overlayMaterial = null;
            }

            // Overlay mesh.
            if (_overlayMesh)
            {
                _log.Info("destroying existing overlay mesh");
                UnityEngine.Object.DestroyImmediate(_overlayMesh);
                _overlayMesh = null;
            }

            // GameObject.
            if (_overlayObject)
            {
                _log.Info("destroying existing overlay object");
                UnityEngine.Object.DestroyImmediate(_overlayObject);
                _overlayObject = null;
            }
        }
    }
}
