//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3
//------------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;
using MoveRotateResize.Dynamic.PuddlePainter;

namespace MoveRotateResize.EditorInterface
{
	public class ToolsMenu
	{
		[MenuItem("Tools/MoveRotateResize/DYNAMIC- RainWetness/DYNAMIC- RainWetness Systems Prefab")]
		public static void CreateDYNAMICRainWetnessQuickStart()
		{
			string prefabPath = "Assets/MoveRotateResize/DYNAMIC- RainWetness/All Prefabs/DYNAMIC- RainWetness Systems.prefab";
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

			if (prefab != null)
			{
				if (GameObject.Find(prefab.name) == null)
				{
					Undo.IncrementCurrentGroup();
					Undo.SetCurrentGroupName("Create = DYNAMIC- RainWetness Prefab");

					GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
					if (instance != null)
					{
						Undo.RegisterCreatedObjectUndo(instance, "Create = DYNAMIC- Rain Wetness Prefab");
						
						//Undo.IncrementCurrentGroup();
						//CreatePuddleGroup.CreatePuddleGroupFromAnotherScript("Default");
						//Undo.SetCurrentGroupName("Create = Puddle Group");

						instance.transform.position = Vector3.zero;
					}
					else
					{
						Debug.LogError("Failed to instantiate = DYNAMIC- RainWetness Prefab.");
					}

					Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
				}
				else
				{
					Debug.LogWarning("An instance with the name " + prefab.name + " already exists in the scene.");
				}
			}
			else
			{
				Debug.LogError("DYNAMIC- RainWetness Prefab not found at path: " + prefabPath);
			}

		}


		[MenuItem("Tools/MoveRotateResize/DYNAMIC- Pooling/Spawn Points/Interior Audio")]
		public static void CreateSpawnPointInteriorAudio()
		{
			string prefabPath = "Assets/MoveRotateResize/Dynamic- Object Pooling/Prefabs/Spawn Points/Spawn Point- Interior Audio.prefab";
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

			if (prefab != null)
			{
				Undo.IncrementCurrentGroup();
				Undo.SetCurrentGroupName("Create Interior Audio Prefab");

				GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
				if (instance != null)
				{
					SceneView sceneView = SceneView.lastActiveSceneView;
					if (sceneView != null)
					{
						Camera sceneCamera = sceneView.camera;
						if (sceneCamera != null)
						{
							instance.transform.position = sceneCamera.transform.position + sceneCamera.transform.forward * 10f;
						}
					}

					Undo.RegisterCreatedObjectUndo(instance, "Create Interior Audio Prefab");
				}
				else
				{
					Debug.LogError("Failed to instantiate Interior Audio Prefab.");
				}

				Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
			}
			else
			{
				Debug.LogError("Interior Audio Prefab not found at path: " + prefabPath);
			}
		}


		[MenuItem("Tools/MoveRotateResize/DYNAMIC- Controllers/1st Person Controller")]
		public static void CreateFirstPersonContoller()
		{
			string prefabPath = "Assets/MoveRotateResize/DYNAMIC- 1st Person Controller/Prefabs/1st Person Controller.prefab";
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

			if (prefab != null)
			{
				Undo.IncrementCurrentGroup();
				Undo.SetCurrentGroupName("Create Player Prefab");

				GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
				if (instance != null)
				{
					SceneView sceneView = SceneView.lastActiveSceneView;
					if (sceneView != null)
					{
						Camera sceneCamera = sceneView.camera;
						if (sceneCamera != null)
						{
							instance.transform.position = sceneCamera.transform.position + sceneCamera.transform.forward * 10f;
						}
					}

					Undo.RegisterCreatedObjectUndo(instance, "Create Player Prefab");
				}
				else
				{
					Debug.LogError("Failed to instantiate Player Prefab.");
				}

				Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
			}
			else
			{
				Debug.LogError("Player not found at path: " + prefabPath);
			}
		}



		[MenuItem("Tools/MoveRotateResize/DYNAMIC- Controllers/Free Fly Camera")]
		public static void CreateFreeFlyCamera()
		{
			string prefabPath = "Assets/MoveRotateResize/DYNAMIC- Free Fly Camera/Prefabs/DYNAMIC- Free Fly Camera.prefab";
			GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

			if (prefab != null)
			{
				Undo.IncrementCurrentGroup();
				Undo.SetCurrentGroupName("Create Free Fly Camera Prefab");

				GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
				if (instance != null)
				{
					SceneView sceneView = SceneView.lastActiveSceneView;
					if (sceneView != null)
					{
						Camera sceneCamera = sceneView.camera;
						if (sceneCamera != null)
						{
							instance.transform.position = sceneCamera.transform.position + sceneCamera.transform.forward * 10f;
						}
					}

					Undo.RegisterCreatedObjectUndo(instance, "Create Free Fly Camera Prefab");
				}
				else
				{
					Debug.LogError("Failed to instantiate Create Free Fly Camera Prefab.");
				}

				Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
			}
			else
			{
				Debug.LogError("Create Free Fly Camera not found at path: " + prefabPath);
			}
		}
	}
}