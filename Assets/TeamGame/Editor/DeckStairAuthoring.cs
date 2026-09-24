#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Team15.Editor
{
    public static class DeckStairAuthoring
    {
        const string RootName = "Deck walkable stair surfaces";
        const string FenceMeshPath = "Assets/TeamGame/Generated/DeckBowPassage.asset";

        // Called by the Deck authoring pass. The caller owns saving the scene.
        public static void Apply()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "DeckScene") throw new InvalidOperationException("Open DeckScene before applying Deck stair surfaces.");
            var room = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<TeamRoom>(true)).Single(r => r.roomIndex == 0);
            var existing = room.transform.Find(RootName);
            if (existing) Object.DestroyImmediate(existing.gameObject);
            var root = new GameObject(RootName).transform;
            root.SetParent(room.transform, false);
            root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.localScale = Vector3.one;

            var aft = StairMesh("Aft", 1.60f, new[] {
                new Vector2(-.745f,4.690f), new Vector2(-4.370f,9.015f), new Vector2(-4.950f,9.015f),
                new Vector2(-5.340f,9.305f), new Vector2(-5.980f,9.305f) });
            var forward = StairMesh("Forward", 1.60f, new[] {
                new Vector2(7.950f,4.690f), new Vector2(9.130f,6.115f), new Vector2(9.700f,6.115f),
                new Vector2(10.060f,6.405f), new Vector2(10.550f,6.405f) });
            var lower = StairMesh("Lower", 1.50f, new[] {
                new Vector2(-4.400f,-.045f), new Vector2(-7.790f,4.005f), new Vector2(-8.450f,4.005f),
                new Vector2(-8.950f,4.310f), new Vector2(-9.220f,4.310f) });
            foreach (float x in new[] { -.596f, 7.101f })
            {
                string side = x < 3 ? "Port" : "Starboard";
                StairRun(root, side + " aft stairs", x, aft);
            }
            foreach (float x in new[] { -.600f, 7.105f })
            {
                string side = x < 3 ? "Port" : "Starboard";
                StairRun(root, side + " forward stairs", x, forward);
            }
            foreach (float x in new[] { .512f, 5.993f })
            {
                string side = x < 3 ? "Port" : "Starboard";
                StairRun(root, side + " lower stairs", x, lower);
            }

            var originalStairs = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshCollider>(true))
                .Where(c => c.name == "StylShip_StairsTop" || c.name == "StylShip_StairsSmall" || c.name == "StylShip_StairsBot").ToArray();
            if (originalStairs.Length != 3) throw new InvalidOperationException("Expected exactly three original stair MeshColliders.");
            foreach (var collision in originalStairs)
            {
                collision.enabled = false;
                EditorUtility.SetDirty(collision);
                PrefabUtility.RecordPrefabInstancePropertyModifications(collision);
            }

            OpenBowPassage(scene);
            Surface(root, "Bow beam access bridge", 3.252f, .86f, 21.500f, 6.375f, 22.600f, 7.615f, true);
            Surface(root, "Bow beam landing", 3.252f, .70f, 22.570f, 7.615f, 24.950f, 7.615f, true);
            Physics.SyncTransforms();
            Debug.Log("[DeckStairs] Six continuous stair walk surfaces now replace the three original stepped colliders. Deck floors and the bow access remain in place. Scene not saved by this method.");
        }

        // One continuous sheet has no internal vertical faces for a capsule to catch on.
        // Each of the three profiles is shared between port and starboard.
        static Mesh StairMesh(string id, float width, Vector2[] profile)
        {
            var vertices = new Vector3[profile.Length * 2];
            var triangles = new int[(profile.Length - 1) * 6];
            for (int i = 0; i < profile.Length; i++)
            {
                vertices[i * 2] = new Vector3(-width * .5f, profile[i].y, profile[i].x);
                vertices[i * 2 + 1] = new Vector3(width * .5f, profile[i].y, profile[i].x);
            }
            for (int i = 0; i < profile.Length - 1; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                bool forward = profile[i + 1].x > profile[i].x;
                int at = i * 6;
                triangles[at] = a; triangles[at + 1] = forward ? c : b; triangles[at + 2] = forward ? b : c;
                triangles[at + 3] = b; triangles[at + 4] = forward ? c : d; triangles[at + 5] = forward ? d : c;
            }
            var mesh = new Mesh { name = "Deck " + id + " continuous walk surface", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string path = "Assets/TeamGame/Generated/Deck" + id + "WalkSurface.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved) { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
            else { saved = mesh; AssetDatabase.CreateAsset(saved, path); }
            return saved;
        }
        static void StairRun(Transform parent, string name, float x, Mesh mesh)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(x, 0, 0);
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = false;
        }

        // End points describe the top walking surface, not the collider centre.
        static void Surface(Transform parent, string name, float x, float width, float z0, float y0, float z1, float y1, bool visible = false)
        {
            Vector3 start = new Vector3(x, y0, z0), end = new Vector3(x, y1, z1);
            Vector3 along = (end - start).normalized;
            Vector3 normal = Vector3.ProjectOnPlane(Vector3.up, along).normalized;
            const float thickness = .08f;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation((start + end) * .5f - normal * (thickness * .5f), Quaternion.LookRotation(along, normal));
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(width, thickness, Vector3.Distance(start, end) + .025f);
            if (visible)
            {
                var planks = GameObject.CreatePrimitive(PrimitiveType.Cube);
                planks.name = "Wooden walking surface";
                planks.transform.SetParent(go.transform, false);
                planks.transform.localScale = box.size;
                Object.DestroyImmediate(planks.GetComponent<Collider>());
                planks.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/TeamGame/Generated/RouteSign.mat");
            }
        }

        struct Vertex
        {
            public Vector3 position, normal;
            public Vector4 tangent;
            public Vector2 uv, uv2;
            public Color color;
            public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex {
                position=Vector3.Lerp(a.position,b.position,t), normal=Vector3.Lerp(a.normal,b.normal,t).normalized,
                tangent=Vector4.Lerp(a.tangent,b.tangent,t), uv=Vector2.Lerp(a.uv,b.uv,t), uv2=Vector2.Lerp(a.uv2,b.uv2,t), color=Color.Lerp(a.color,b.color,t)
            };
        }

        static void OpenBowPassage(Scene scene)
        {
            var filter = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshFilter>(true)).Single(f => f.name == "StylShip_FencingFront");
            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Stylized_Pirate_Ship/StylShip_Unity.prefab");
            var source = sourcePrefab.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "StylShip_FencingFront").sharedMesh;
            var positions=source.vertices;var normals=source.normals;var tangents=source.tangents;var uv=source.uv;var uv2=source.uv2;var colors=source.colors;
            var input=new Vertex[positions.Length];
            for(int i=0;i<input.Length;i++) input[i]=new Vertex {
                position=positions[i], normal=normals.Length==positions.Length?normals[i]:Vector3.up,
                tangent=tangents.Length==positions.Length?tangents[i]:new Vector4(1,0,0,1), uv=uv.Length==positions.Length?uv[i]:Vector2.zero,
                uv2=uv2.Length==positions.Length?uv2[i]:Vector2.zero, color=colors.Length==positions.Length?colors[i]:Color.white
            };
            var output=new List<Vertex>();var indices=new List<int>[source.subMeshCount];
            // Subtract only a 1 m corridor at the tip. Keep the rest of the imported rail mesh intact.
            Func<Vertex,float>[] planes = {
                v => filter.transform.TransformPoint(v.position).x - 2.752f,
                v => 3.752f - filter.transform.TransformPoint(v.position).x,
                v => filter.transform.TransformPoint(v.position).z - 22.450f,
                v => 23.600f - filter.transform.TransformPoint(v.position).z
            };
            for(int sub=0;sub<source.subMeshCount;sub++)
            {
                indices[sub]=new List<int>();var tris=source.GetTriangles(sub);
                for(int i=0;i<tris.Length;i+=3)
                {
                    var remainder=new List<Vertex>{input[tris[i]],input[tris[i+1]],input[tris[i+2]]};
                    foreach(var plane in planes)
                    {
                        Emit(Clip(remainder,plane,false),output,indices[sub]);
                        remainder=Clip(remainder,plane,true);
                        if(remainder.Count<3) break;
                    }
                }
            }
            var mesh = new Mesh { name="Deck front rail with bow access", indexFormat=UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(output.Select(v=>v.position).ToList()); mesh.SetNormals(output.Select(v=>v.normal).ToList());
            mesh.SetTangents(output.Select(v=>v.tangent).ToList());mesh.SetUVs(0,output.Select(v=>v.uv).ToList());
            if(uv2.Length==positions.Length)mesh.SetUVs(1,output.Select(v=>v.uv2).ToList());
            if(colors.Length==positions.Length)mesh.SetColors(output.Select(v=>v.color).ToList());
            mesh.subMeshCount=source.subMeshCount;
            for(int sub=0;sub<indices.Length;sub++)mesh.SetTriangles(indices[sub],sub,false);
            mesh.RecalculateBounds();
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(FenceMeshPath);
            if(saved){EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(saved);}
            else{saved=mesh;AssetDatabase.CreateAsset(saved,FenceMeshPath);}
            filter.sharedMesh=saved;
            var collision=filter.GetComponent<MeshCollider>();
            if(!collision)throw new InvalidOperationException("Expected the front rail MeshCollider.");
            collision.sharedMesh=null;collision.convex=false;collision.sharedMesh=saved;
            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            PrefabUtility.RecordPrefabInstancePropertyModifications(collision);
        }

        static List<Vertex> Clip(List<Vertex> polygon,Func<Vertex,float> distance,bool keepInside)
        {
            var result=new List<Vertex>();if(polygon.Count==0)return result;
            Vertex previous=polygon[polygon.Count-1];float prevDistance=distance(previous);bool prevKept=keepInside?prevDistance>=0:prevDistance<0;
            foreach(var current in polygon)
            {
                float currDistance=distance(current);bool currKept=keepInside?currDistance>=0:currDistance<0;
                if(currKept!=prevKept)result.Add(Vertex.Lerp(previous,current,prevDistance/(prevDistance-currDistance)));
                if(currKept)result.Add(current);
                previous=current;prevDistance=currDistance;prevKept=currKept;
            }
            return result;
        }
        static void Emit(List<Vertex> polygon,List<Vertex> vertices,List<int> indices)
        {
            if(polygon.Count<3)return;int first=vertices.Count;vertices.AddRange(polygon);
            for(int i=1;i<polygon.Count-1;i++){indices.Add(first);indices.Add(first+i);indices.Add(first+i+1);}
        }
    }
}
#endif
