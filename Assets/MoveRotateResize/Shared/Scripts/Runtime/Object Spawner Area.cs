//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.0.0
//------------------------------------------------------------------------------
using UnityEngine;

namespace MoveRotateResize.General
{
    [RequireComponent(typeof(BoxCollider))]
    public class ObjectSpawnerArea : MonoBehaviour
    {
        public GameObject objectToSpawn;
        public int numberOfObjects = 10;
        private Vector3 spawnAreaSize;
        public float spawnedObjectScale = 1f;

        void Awake()
        {
            CalculateSpawnAreaSize();
            SpawnObjects();
        }

        void CalculateSpawnAreaSize()
        {
            BoxCollider collider = GetComponent<BoxCollider>();
            if (collider != null)
            {
                spawnAreaSize = collider.size;
            }
            else
            {
                Debug.LogError("Box Collider not found on the GameObject.");
                spawnAreaSize = Vector3.one;
            }
        }

        void SpawnObjects()
        {
            for (int i = 0; i < numberOfObjects; i++)
            {
                Vector3 spawnPoint = GetRandomSpawnPoint();
                GameObject spawnedObject = Instantiate(objectToSpawn, spawnPoint, Quaternion.identity);
                spawnedObject.transform.localScale = Vector3.one * spawnedObjectScale;
                spawnedObject.transform.SetParent(transform);
            }
        }

        Vector3 GetRandomSpawnPoint()
        {
            Vector3 center = transform.position;
            Vector3 randomPoint = center + new Vector3(
                Random.Range(-spawnAreaSize.x / 2, spawnAreaSize.x / 2),
                Random.Range(-spawnAreaSize.y / 2, spawnAreaSize.y / 2),
                Random.Range(-spawnAreaSize.z / 2, spawnAreaSize.z / 2)
            );
            return randomPoint;
        }
    }
}