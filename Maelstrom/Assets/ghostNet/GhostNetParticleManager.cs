using UnityEngine;

namespace Maelstrom.Unity
{
    /// <summary>
    /// Static class for managing particle system operations in GhostNet visualization
    /// </summary>
    public static class GhostNetParticleManager
    {
        /// <summary>
        /// Handle particle emission for ##OTHERS## datapoints
        /// </summary>
        /// <param name="particles">The particle system to use for emission</param>
        /// <param name="dataPoint">The data point containing tweet count information</param>
        /// <param name="currentMaelstrom">Current maelstrom value for particle properties</param>
        public static void HandleOthersDataPoint(ParticleSystem particles, GhostNetDataPoint dataPoint, float currentMaelstrom)
        {
            if (particles == null)
            {
                Debug.LogWarning("Particle system not assigned, cannot handle ##OTHERS## datapoint");
                return;
            }

            // Calculate number of particles to emit based on nb_tweets
            // Scale down the number to reasonable particle count (max 1000 particles)
            int particleCount = dataPoint.nb_tweets;
            
            if (particleCount > 0)
            {
                ConfigureParticleSystem(particles, particleCount, currentMaelstrom);
                
                Debug.Log($"Emitted {particleCount} particles for ##OTHERS## datapoint with {dataPoint.nb_tweets} tweets");
            }
        }

        /// <summary>
        /// Configure particle system properties for emission
        /// </summary>
        /// <param name="particles">The particle system to configure</param>
        /// <param name="particleCount">Number of particles to emit</param>
        /// <param name="currentMaelstrom">Current maelstrom value for particle properties</param>
        public static void ConfigureParticleSystem(ParticleSystem particles, int particleCount, float currentMaelstrom)
        {
            // Configure particle system for emission
            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = particleCount / 10; // Disable continuous emission


            // Set particle system properties based on maelstrom
            var main = particles.main;
            main.startLifetime = 10.0f; // Longer lifetime with higher maelstrom
            //main.duration = 10f;
            main.startSpeed = 65f + currentMaelstrom * 100f; // Faster particles with higher maelstrom
            main.startSize = 2f + currentMaelstrom * 5f; // Larger particles with higher maelstrom
            main.maxParticles = 10000; // Set max particles to prevent memory issues

            // Set color based on maelstrom (similar to display objects)
            SetParticleColor(particles, currentMaelstrom);
        }

        /// <summary>
        /// Set particle color based on maelstrom value
        /// </summary>
        /// <param name="particles">The particle system to configure</param>
        /// <param name="currentMaelstrom">Current maelstrom value for color calculation</param>
        public static void SetParticleColor(ParticleSystem particles, float currentMaelstrom)
        {
            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { 
                    new GradientColorKey(new Color(1 - currentMaelstrom, 1 - currentMaelstrom, 1), 0.0f),
                    new GradientColorKey(new Color(1 - currentMaelstrom, 1 - currentMaelstrom, 1), 1.0f)
                },
                new GradientAlphaKey[] { 
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorOverLifetime.color = gradient;
        }

        /// <summary>
        /// Clear all particles from the particle system
        /// </summary>
        /// <param name="particles">The particle system to clear</param>
        public static void ClearParticles(ParticleSystem particles)
        {
            if (particles != null)
            {
                particles.Clear();
            }
        }

        /// <summary>
        /// Get the position of the particle system as Vector2
        /// </summary>
        /// <param name="particles">The particle system to get position from</param>
        /// <returns>Vector2 position of the particle system</returns>
        public static Vector2 GetParticleSystemPosition(ParticleSystem particles)
        {
            if (particles != null)
            {
                Vector3 position = particles.transform.position;
                return new Vector2(position.x, position.y);
            }
            return Vector2.zero;
        }

        /// <summary>
        /// Check if particle system is valid and ready for use
        /// </summary>
        /// <param name="particles">The particle system to check</param>
        /// <returns>True if particle system is valid, false otherwise</returns>
        public static bool IsParticleSystemValid(ParticleSystem particles)
        {
            return particles != null && particles.gameObject.activeInHierarchy;
        }
    }
}
