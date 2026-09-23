using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using TMPro;

namespace BlackTide
{
    // Primitive-based art keeps the project editable and independent of paid asset packs.
    public static class SceneKit
    {
        public static GameObject Group(string name, Transform parent = null, Vector3 position = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            return go;
        }
        public static GameObject Shape(string name, PrimitiveType type, Transform parent,
            Vector3 position, Vector3 scale, Material material, bool collision = false, Vector3 rotation = default)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.transform.localEulerAngles = rotation;
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
            var col = go.GetComponent<Collider>();
            if (!collision) col.enabled = false;
            return go;
        }
        public static GameObject Box(string name, Transform parent, Vector3 at, Vector3 size,
            Material mat, bool solid = false, Vector3 rot = default)
            => Shape(name, PrimitiveType.Cube, parent, at, size, mat, solid, rot);

        public static Material Lit(string name, Color color, float metal = 0f, float smooth = .3f,
            string texture = null, Vector2 tiling = default, string normal = null, bool glow = false)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Standard");
            var m = new Material(shader) { name = name, color = color, enableInstancing = true };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Glossiness", smooth);
            if (texture != null)
            {
                Texture2D t = Resources.Load<Texture2D>("Pirate/Textures/" + texture);
                m.SetTexture("_BaseMap", t); m.SetTexture("_MainTex", t);
                Vector2 uv = tiling == default ? Vector2.one : tiling;
                m.SetTextureScale("_BaseMap", uv); m.SetTextureScale("_MainTex", uv);
            }
            if (normal != null)
            {
                m.SetTexture("_BumpMap", Resources.Load<Texture2D>("Pirate/Textures/" + normal));
                m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP");
            }
            if (glow)
            {
                m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * 1.7f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return m;
        }
        public static GameObject Ring(string name, Transform parent, Vector3 at, float radius,
            float tube, Material material, Vector3 rotation = default)
        {
            const int segments = 48, sides = 8;
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = 2f * Mathf.PI * i / segments;
                for (int j = 0; j <= sides; j++)
                {
                    float b = 2f * Mathf.PI * j / sides;
                    float r = radius + tube * Mathf.Cos(b);
                    vertices.Add(new Vector3(r * Mathf.Cos(a), tube * Mathf.Sin(b), r * Mathf.Sin(a)));
                    uv.Add(new Vector2((float)i / segments, (float)j / sides));
                    if (i < segments && j < sides)
                    {
                        int k = i * (sides + 1) + j;
                        triangles.AddRange(new[] { k, k + 1, k + sides + 1, k + 1, k + sides + 2, k + sides + 1 });
                    }
                }
            }
            var mesh = new Mesh { name = name + " Mesh" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            var go = Group(name, parent, at); go.transform.localEulerAngles = rotation;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }
        public static TMP_Text Panel(string name, Transform parent, Vector3 at, Vector2 size,
            string text, int fontSize = 44, Vector3 rotation = default)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(parent, false); go.transform.localPosition = at;
            go.transform.localEulerAngles = rotation; go.transform.localScale = Vector3.one * .003f;
            var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = size / .003f;
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var child = new GameObject("Clear world-space SDF text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            child.transform.SetParent(go.transform, false);
            var cr = child.GetComponent<RectTransform>(); cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one;
            cr.offsetMin = new Vector2(24f, 20f); cr.offsetMax = new Vector2(-24f, -20f);
            var label = child.GetComponent<TextMeshProUGUI>();
            label.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            label.fontSize = fontSize; label.lineSpacing = 7f; label.richText = true;
            label.alignment = TextAlignmentOptions.Center; label.color = new Color(.97f, .9f, .71f);
            label.enableWordWrapping = true; label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false; label.text = text;
            return label;
        }
        public static ParticleSystem Burst(string name, Transform parent, Vector3 at, Material material)
        {
            var go = Group(name, parent, at); var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = false; main.playOnAwake = false; main.duration = .5f;
            main.startLifetime = .8f; main.startSpeed = 1.5f; main.startSize = .06f;
            main.startColor = new Color(1f, .73f, .2f); main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .12f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            return ps;
        }
        public static AudioSource Sound(string name, Transform parent, Vector3 at, string clip, bool loop = false)
        {
            var go = Group(name, parent, at); var audio = go.AddComponent<AudioSource>();
            audio.clip = Resources.Load<AudioClip>("Pirate/Audio/" + clip);
            audio.playOnAwake = loop; audio.loop = loop; audio.spatialBlend = 1f;
            audio.rolloffMode = AudioRolloffMode.Logarithmic; audio.minDistance = 1.5f;
            audio.maxDistance = 18f; audio.volume = loop ? .2f : .65f; audio.dopplerLevel = 0f;
            return audio;
        }
    }
}
