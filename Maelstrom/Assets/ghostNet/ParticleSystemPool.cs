using UnityEngine;

namespace Maelstrom.Unity
{
    /// <summary>
    /// Manages a pool of particle systems to prevent property changes from affecting previously emitted particles.
    /// When spawning particles with different properties, it cycles to the next particle system in the pool.
    /// </summary>
    public class ParticleSystemPool : MonoBehaviour
    {
        [SerializeField] private ParticleSystem templateParticleSystem;
        [SerializeField] private int poolSize = 10;

        private ParticleSystem[] _particleSystems;
        private int _currentIndex;
        private bool _isInitialized;

        private void Start()
        {
            if (!_isInitialized)
                Initialize();
        }

        public void Initialize()
        {
            if (_isInitialized)
                return;

            if (templateParticleSystem == null)
            {
                AppLogger.LogWarning("ParticleSystemPool: Template particle system not assigned");
                return;
            }

            _particleSystems = new ParticleSystem[poolSize];

            for (int i = 0; i < poolSize; i++)
            {
                var instance = Instantiate(templateParticleSystem, transform);
                instance.name = $"ParticleSystem_{i}";
                instance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _particleSystems[i] = instance;
            }

            templateParticleSystem.gameObject.SetActive(false);
            _currentIndex = 0;
            _isInitialized = true;

            AppLogger.Log($"ParticleSystemPool initialized with {poolSize} particle systems");
        }

        /// <summary>
        /// Spawns particles using the next particle system in the pool.
        /// Stops the current system's emission and starts the next one with the given properties.
        /// </summary>
        /// <param name="count">Number of particles to emit</param>
        /// <param name="maelstrom">Current maelstrom value for particle properties</param>
        public void SpawnDataPoints(int count, float maelstrom)
        {
            if (!_isInitialized || _particleSystems == null)
            {
                AppLogger.LogWarning("ParticleSystemPool: Not initialized");
                return;
            }

            var currentSystem = _particleSystems[_currentIndex];
            currentSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            _currentIndex = (_currentIndex + 1) % poolSize;

            var nextSystem = _particleSystems[_currentIndex];
            GhostNetParticleManager.ConfigureParticleSystem(nextSystem, count, maelstrom);
            nextSystem.Play();
        }

        /// <summary>
        /// Stops all particle systems in the pool
        /// </summary>
        public void StopAll()
        {
            if (_particleSystems == null)
                return;

            foreach (var ps in _particleSystems)
            {
                if (ps != null)
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        /// <summary>
        /// Clears all particles from all systems in the pool
        /// </summary>
        public void ClearAll()
        {
            if (_particleSystems == null)
                return;

            foreach (var ps in _particleSystems)
            {
                if (ps != null)
                    ps.Clear();
            }
        }
    }
}
