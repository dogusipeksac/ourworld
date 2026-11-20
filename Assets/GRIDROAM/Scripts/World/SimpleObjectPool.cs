using System.Collections.Generic;
using UnityEngine;

namespace Gridroam.World
{
    /// <summary>
    /// Simple zero-churn GameObject pool for ambient traffic vehicles and pedestrians.
    /// Pre-warms instances at startup so gameplay never triggers Instantiate/Destroy GC spikes.
    /// </summary>
    public class SimpleObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Transform _parent;
        private readonly Stack<GameObject> _inactive = new Stack<GameObject>();

        public SimpleObjectPool(GameObject prefab, Transform parent, int prewarmCount)
        {
            _prefab = prefab;
            _parent = parent;
            Prewarm(prewarmCount);
        }

        private void Prewarm(int count)
        {
            if (_prefab == null) return;
            for (int i = 0; i < count; i++)
            {
                GameObject instance = Object.Instantiate(_prefab, _parent);
                instance.SetActive(false);
                _inactive.Push(instance);
            }
        }

        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            GameObject instance = _inactive.Count > 0
                ? _inactive.Pop()
                : Object.Instantiate(_prefab, _parent);

            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            return instance;
        }

        public void Release(GameObject instance)
        {
            if (instance == null) return;
            instance.SetActive(false);
            _inactive.Push(instance);
        }
    }
}
