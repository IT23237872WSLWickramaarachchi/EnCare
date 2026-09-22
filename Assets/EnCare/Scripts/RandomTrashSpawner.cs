using System.Collections.Generic;
using UnityEngine;

namespace EnCare
{
    public sealed class RandomTrashSpawner : MonoBehaviour
    {
        [Tooltip("5-10 different prefabs. Each needs TrashItem on its root.")]
        [SerializeField] TrashItem[] prefabPool;
        [Tooltip("Exactly one unique empty Transform per required item; 10 for this level.")]
        [SerializeField] Transform[] spawnPoints;
        [SerializeField] bool overrideGlowColor = true;
        [ColorUsage(false, true)] [SerializeField] Color levelGlowColor = new Color(0.1f, 1f, 0.8f, 1f);
        bool spawned;

        public bool Spawn(CleanupMission mission)
        {
            if (spawned || mission == null) return false;
            if (prefabPool == null || prefabPool.Length == 0)
            {
                Debug.LogError("EnCare: assign at least one prefab to prefabPool.", this);
                return false;
            }
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                Debug.LogError("EnCare: assign spawnPoints to RandomTrashSpawner.", this);
                return false;
            }

            var validPoints = new List<Transform>();
            foreach (Transform point in spawnPoints)
            {
                if (point != null && !validPoints.Contains(point))
                    validPoints.Add(point);
            }

            if (validPoints.Count == 0)
            {
                Debug.LogError("EnCare: no valid spawn points found.", this);
                return false;
            }

            var validPrefabs = new List<TrashItem>();
            foreach (TrashItem prefab in prefabPool)
            {
                if (prefab != null) validPrefabs.Add(prefab);
            }

            if (validPrefabs.Count == 0)
            {
                Debug.LogError("EnCare: all prefabPool entries are null.", this);
                return false;
            }

            spawned = true;
            int countToSpawn = Mathf.Min(mission.targetCount, validPoints.Count);
            mission.targetCount = countToSpawn;

            // Shuffle bags make every model appear before cycling through the pool again.
            var bag = new List<TrashItem>();
            for (int i = 0; i < countToSpawn; i++)
            {
                if (bag.Count == 0) bag.AddRange(validPrefabs);
                int index = Random.Range(0, bag.Count);
                TrashItem chosen = bag[index];
                bag.RemoveAt(index);
                Transform point = validPoints[i];
                TrashItem item = Instantiate(chosen, point.position, point.rotation);
                item.Initialize(mission);
                if (overrideGlowColor)
                    foreach (TrashGlow glow in item.GetComponentsInChildren<TrashGlow>())
                        glow.SetColor(levelGlowColor);
            }
            return true;
        }
    }
}
