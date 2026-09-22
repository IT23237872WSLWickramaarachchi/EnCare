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
            if (prefabPool == null || prefabPool.Length == 0 || spawnPoints == null ||
                spawnPoints.Length != mission.targetCount)
            {
                Debug.LogError("EnCare: assign a prefab pool and exactly Target Count spawn points.", this);
                return false;
            }
            var uniquePoints = new HashSet<Transform>();
            foreach (Transform point in spawnPoints)
                if (point == null || !uniquePoints.Add(point))
                {
                    Debug.LogError("EnCare: spawn points must be assigned and unique.", this);
                    return false;
                }
            foreach (TrashItem prefab in prefabPool)
                if (prefab == null || !prefab.gameObject.activeSelf ||
                    prefab.GetComponentInChildren<Collider>() == null)
                {
                    Debug.LogError("EnCare: all pool entries need an active TrashItem prefab with a collider.", this);
                    return false;
                }
            spawned = true;
            // Shuffle bags make every model appear before cycling through the pool again.
            // Repeats are needed if there are fewer prefabs than spawn points.
            var bag = new List<TrashItem>();
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (bag.Count == 0) bag.AddRange(prefabPool);
                int index = Random.Range(0, bag.Count);
                TrashItem chosen = bag[index];
                bag.RemoveAt(index);
                Transform point = spawnPoints[i];
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
