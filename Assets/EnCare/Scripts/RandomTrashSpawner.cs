using System.Collections.Generic;
using UnityEngine;

namespace EnCare
{
    public sealed class RandomTrashSpawner : MonoBehaviour
    {
        [Tooltip("Prefab pool of trash items. Each needs TrashItem on its root.")]
        [SerializeField] TrashItem[] prefabPool;

        [Tooltip("Available spawn positions across the map (e.g. 15 spawn points).")]
        [SerializeField] Transform[] spawnPoints;

        [Header("Spawn Configuration")]
        [Tooltip("How many trash items should be spawned at a time. The spawner will randomly pick this many positions out of all available spawn points.")]
        [Min(1)]
        [SerializeField] int spawnCount = 10;

        [Header("Visual Highlighting")]
        [SerializeField] bool overrideGlowColor = true;
        [ColorUsage(false, true)] [SerializeField] Color levelGlowColor = new Color(0.1f, 1f, 0.8f, 1f);

        bool spawned;

        public int SpawnCount
        {
            get => spawnCount;
            set => spawnCount = Mathf.Max(1, value);
        }

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

            // Randomize spawn points so any N random positions out of total available positions are chosen
            for (int i = validPoints.Count - 1; i > 0; i--)
            {
                int r = Random.Range(0, i + 1);
                var temp = validPoints[i];
                validPoints[i] = validPoints[r];
                validPoints[r] = temp;
            }

            // Determine how many items to spawn (default 10, clamped to available valid points)
            int countToSpawn = Mathf.Clamp(spawnCount, 1, validPoints.Count);
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
                {
                    foreach (TrashGlow glow in item.GetComponentsInChildren<TrashGlow>())
                        glow.SetColor(levelGlowColor);

                    foreach (var eWasteGlow in item.GetComponentsInChildren<EnCare.VR.EWasteGlow>())
                        eWasteGlow.SetColor(levelGlowColor);
                }
            }
            return true;
        }
    }
}
