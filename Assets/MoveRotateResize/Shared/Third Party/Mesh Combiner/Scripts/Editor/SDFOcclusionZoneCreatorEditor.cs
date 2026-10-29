//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3
//------------------------------------------------------------------------------
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using MoveRotateResize.Dynamic.MultiTag;

namespace MoveRotateResize.Dynamic.SDFOcclusionZoneCreator
{
	[CustomEditor(typeof(SDFOcclusionZoneCreator))]
	public class SDFOcclusionZoneCreatorEditor : Editor
	{
		Vector3 boundingBoxSize;
		Vector3 boundingBoxCenter;

		public override void OnInspectorGUI()
		{
			SDFOcclusionZoneCreator meshCombiner = (SDFOcclusionZoneCreator)target;

			DrawDefaultInspector();


            GUILayout.Space(10);


			if (GUILayout.Button("Create SDF Occlusion Zone"))
			{
				EnsureComponentsExist(meshCombiner);

				meshCombiner.CombineMeshes(true);
				AutoSaveCombinedMesh(meshCombiner);
				CreateNewCombinedMeshObject(meshCombiner);
			}
		}

		private bool IsValidPath(string folderPath)
		{
			string pattern = "[:*?\"<>|]";
			Regex regex = new Regex(pattern);
			return (!regex.IsMatch(folderPath));
		}

		void AutoSaveCombinedMesh(SDFOcclusionZoneCreator meshCombiner)
		{
			Mesh mesh = meshCombiner.GetComponent<MeshFilter>().sharedMesh;

			if (mesh != null)
			{
				string originalName = mesh.name;
				mesh.name = "SDF Occlusion Zone_" + originalName;
				string folderPath = meshCombiner.FolderPath;
				SaveCombinedMesh(mesh, folderPath);
			}
		}

		string SaveCombinedMesh(Mesh mesh, string folderPath)
		{
			bool meshIsSaved = AssetDatabase.Contains(mesh);

			folderPath = folderPath.Replace('\\', '/');
			if (!meshIsSaved && !AssetDatabase.IsValidFolder("Assets/" + folderPath))
			{
				string[] folderNames = folderPath.Split('/');
				folderNames = folderNames.Where((folderName) => !folderName.Equals("") && !folderName.Equals(" ")).ToArray();

				folderPath = "/";
				for (int i = 0; i < folderNames.Length; i++)
				{
					folderNames[i] = folderNames[i].Trim();
					if (!AssetDatabase.IsValidFolder("Assets" + folderPath + folderNames[i]))
					{
						string folderPathWithoutSlash = folderPath.Substring(0, folderPath.Length - 1);
						AssetDatabase.CreateFolder("Assets" + folderPathWithoutSlash, folderNames[i]);
					}
					folderPath += folderNames[i] + "/";
				}
				folderPath = folderPath.Substring(1, folderPath.Length - 2);
			}

			if (!meshIsSaved)
			{
				string meshPath = "Assets/" + folderPath + "/" + mesh.name + ".asset";
				if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null)
				{
					string baseName = mesh.name;
					int count = 1;
					while (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null)
					{
						meshPath = "Assets/" + folderPath + "/" + baseName + "_" + count + ".asset";
						count++;
					}
				}

				AssetDatabase.CreateAsset(mesh, meshPath);
				AssetDatabase.SaveAssets();
			}

			Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/" + folderPath + "/" + mesh.name + ".asset");
			mesh = savedMesh;
		
			EditorGUIUtility.PingObject(mesh);
			
			return folderPath;
		}

		void CreateNewCombinedMeshObject(SDFOcclusionZoneCreator meshCombiner)
		{
			Mesh combinedMesh = meshCombiner.GetComponent<MeshFilter>().sharedMesh;
			string folderPath = meshCombiner.FolderPath;
			string originalName = meshCombiner.gameObject.name;

			SaveCombinedMesh(combinedMesh, folderPath);

			Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/" + folderPath + "/" + combinedMesh.name + ".asset");
			meshCombiner.GetComponent<MeshFilter>().sharedMesh = savedMesh;

			string newObjectName = "SDF Occlusion Zone_" + originalName;
			meshCombiner.gameObject.name = newObjectName;

			int layerIndex = -1;

			for (int i = 0; i < 32; i++)  
			{
				if ((meshCombiner.occlusionTriggerLayer & (1 << i)) != 0)
				{
					layerIndex = i;
					break;
				}
			}

			if (layerIndex >= 0 && layerIndex <= 31) 
			{
				meshCombiner.gameObject.layer = layerIndex;
			}
			else
			{
				Debug.LogError("Invalid layer index. Layer index must be between 0 and 31.");
			}

			// Add Material
			string materialPath = "Assets/MoveRotateResize/DYNAMIC- Occlusion/Materials/Occlusion Zone.mat";
			Material occlusionMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
			if (occlusionMaterial != null)
			{
				MeshRenderer meshRenderer = meshCombiner.gameObject.GetComponent<MeshRenderer>();
				if (meshRenderer != null)
				{
					meshRenderer.material = occlusionMaterial;
					meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
				}
				else
				{
					Debug.LogWarning("MeshRenderer component is missing on the GameObject.");
				}
			}
			else
			{
				Debug.LogWarning("Material not found at path: " + materialPath);
			}

			// Add Box Collider
			CalculateBoundingBox(meshCombiner);
			BoxCollider boxCollider = meshCombiner.gameObject.GetComponent<BoxCollider>();
			if (boxCollider == null)
			{
				boxCollider = meshCombiner.gameObject.AddComponent<BoxCollider>();
			}
			Vector3 center = boundingBoxCenter;
			Vector3 size = boundingBoxSize;
			boxCollider.center = center;
			boxCollider.size = size;
			boxCollider.isTrigger = true;

			// Add SDF info script
			SDFInfo sdfInfo = meshCombiner.gameObject.AddComponent<SDFInfo>();
			sdfInfo.GetBoxColliderInfo();
			sdfInfo.boxColliderCenter = center;
			sdfInfo.boxColliderSize = size;

			// Add Dynamic Multi Tag script
			DynamicMultiTag dynamicMultiTag = meshCombiner.gameObject.AddComponent<DynamicMultiTag>();
			if (!dynamicMultiTag.HasTag(meshCombiner.occlusionZone))
			{
				dynamicMultiTag.Add(meshCombiner.occlusionZone);
			}


			EditorUtility.SetDirty(meshCombiner.gameObject);
			EditorUtility.SetDirty(savedMesh);

			Undo.RecordObject(meshCombiner, "Remove SDFOcclusionZoneCreator Script");
			DestroyImmediate(meshCombiner);
		}

		public void CalculateBoundingBox(SDFOcclusionZoneCreator meshCombiner)
		{
			MeshRenderer meshRenderer = meshCombiner.gameObject.GetComponent<MeshRenderer>();
			if (meshRenderer != null)
			{
				MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
				if (meshFilter != null)
				{
					Bounds meshBounds = meshFilter.sharedMesh.bounds;

					boundingBoxSize = Vector3.Scale(meshBounds.size, meshCombiner.gameObject.transform.localScale);
					boundingBoxCenter = meshBounds.center;
				}
				else
				{
					Debug.LogError("MeshFilter component not found on the object.");
				}
			}
			else
			{
				Debug.LogError("MeshRenderer component not found on the object.");
			}
		}

		void EnsureComponentsExist(SDFOcclusionZoneCreator meshCombiner)
		{
			if (meshCombiner.gameObject.GetComponent<MeshFilter>() == null)
			{
				Undo.RecordObject(meshCombiner.gameObject, "Add MeshFilter Component");
				meshCombiner.gameObject.AddComponent<MeshFilter>();
			}

			if (meshCombiner.gameObject.GetComponent<MeshRenderer>() == null)
			{
				Undo.RecordObject(meshCombiner.gameObject, "Add MeshRenderer Component");
				meshCombiner.gameObject.AddComponent<MeshRenderer>();
			}
		}
	}
}