using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
{
    public sealed partial class NeighborhoodSports
    {
        private void BuildMotorcycles(Shader shader, Material rubber, Material cream, Material steel)
        {
            Material leather = MaterialFor(shader, "Stitched black motorcycle saddle", new Color(0.065f, 0.043f, 0.037f), 0.27f);
            Material glass = MaterialFor(shader, "Warm headlamp lens", new Color(1f, 0.77f, 0.33f), 0.63f);
            glass.EnableKeyword("_EMISSION");
            glass.SetColor("_EmissionColor", new Color(0.30f, 0.16f, 0.05f));
            AudioClip sharedEngineLoop = CreateMotorcycleEngineLoop();

            // 5 distinct motorcycles spread across Petrol Yolu, Fatih Blvd, Havadis St., Kadife St., and Eyyubi Ave
            // (none clustered on No:48's doorstep, rewarding exploration across the full district!)
            (string objName, string triggerId, Vector3 pos, float yaw, Color paintColor)[] bikes =
            {
                ("PetrolYolu_Motosiklet", "selman_motorcycle",  new Vector3(-24.0f, 0.12f,  -72.2f),  90f, new Color(0.61f, 0.14f, 0.075f)),
                ("Fatih_Motosiklet",      "fatih_motorcycle",   new Vector3( 46.0f, 0.12f,  132.4f), 270f, new Color(0.10f, 0.48f, 0.56f)),
                ("Havadis_Motosiklet",    "havadis_motorcycle", new Vector3( 52.0f, 0.12f,  180.6f), 270f, new Color(0.14f, 0.24f, 0.54f)),
                ("Kadife_Motosiklet",     "kadife_motorcycle",  new Vector3(-42.0f, 0.12f, -180.6f),  90f, new Color(0.78f, 0.50f, 0.12f)),
                ("Eyyubi_Motosiklet",     "eyyubi_motorcycle",  new Vector3(-152.5f,0.12f,  -14.5f),   0f, new Color(0.16f, 0.42f, 0.26f))
            };

            for (int i = 0; i < bikes.Length; i++)
            {
                var m = bikes[i];
                Material paint = MaterialFor(shader, m.objName + " Enamel", m.paintColor, 0.7f);
                BuildMotorcycleAt(m.objName, m.triggerId, m.pos, m.yaw, paint, leather, glass, rubber, cream, steel, sharedEngineLoop);
            }
        }

        private void BuildMotorcycleAt(
            string objName,
            string triggerId,
            Vector3 worldPos,
            float yaw,
            Material paint,
            Material leather,
            Material glass,
            Material rubber,
            Material cream,
            Material steel,
            AudioClip engineLoop)
        {
            Transform motor = Child(transform, objName, worldPos);
            motor.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (Physics.Raycast(motor.position + Vector3.up, Vector3.down, out RaycastHit surface, 1.8f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                motor.position = surface.point + Vector3.up * 0.018f;
            Transform visual = Child(motor, "Leaning frame", Vector3.zero);
            Vector3 front = new Vector3(0f, 0.36f, 0.73f);
            Vector3 rear = new Vector3(0f, 0.36f, -0.73f);
            MeshParts frame = new MeshParts(this);
            frame.Add(paint, _sphere, new Vector3(0f, 0.81f, 0.13f), Quaternion.Euler(-10f, 0f, 0f), new Vector3(0.41f, 0.31f, 0.65f));
            frame.Add(leather, _sphere, new Vector3(0f, 0.842f, -0.37f), Quaternion.identity, new Vector3(0.36f, 0.115f, 0.68f));
            frame.Add(paint, _sphere, new Vector3(0f, 0.72f, -0.60f), Quaternion.identity, new Vector3(0.32f, 0.19f, 0.60f));
            frame.Box(rubber, new Vector3(0f, 0.49f, 0.02f), new Vector3(0.25f, 0.29f, 0.31f));
            for (int fin = 0; fin < 5; fin++)
                frame.Box(steel, new Vector3(0f, 0.47f + fin * 0.034f, 0.06f), new Vector3(0.33f, 0.014f, 0.27f));
            foreach (float side in new[] { -1f, 1f })
            {
                frame.Rod(rubber, _cylinder, new Vector3(side * 0.14f, 0.46f, -0.04f),
                    rear + Vector3.right * (side * 0.14f), 0.034f);
                frame.Box(rubber, new Vector3(side * 0.25f, 0.31f, -0.10f), new Vector3(0.15f, 0.035f, 0.085f));
                frame.Rod(steel, _cylinder, new Vector3(side * 0.195f, 0.34f, 0.23f),
                    new Vector3(side * 0.195f, 0.30f, -0.81f), 0.042f);
                frame.Box(glass, new Vector3(side * 0.175f, 0.72f, -0.91f), new Vector3(0.08f, 0.053f, 0.038f));
            }
            frame.Box(paint, new Vector3(0f, 0.74f, -0.91f), new Vector3(0.23f, 0.071f, 0.047f));
            frame.Box(cream, new Vector3(0f, 0.58f, -0.91f), new Vector3(0.18f, 0.095f, 0.022f));
            frame.Emit(visual, "Motorcycle frame and engine");

            Transform fork = Child(visual, "Steering fork", front);
            MeshParts steering = new MeshParts(this);
            foreach (float side in new[] { -1f, 1f })
            {
                steering.Rod(steel, _cylinder, new Vector3(side * 0.106f, 0f, 0f),
                    new Vector3(side * 0.11f, 0.56f, -0.23f), 0.026f);
                steering.Rod(steel, _cylinder, new Vector3(side * 0.10f, 0.57f, -0.235f),
                    new Vector3(side * 0.31f, 0.70f, -0.38f), 0.016f);
                steering.Rod(rubber, _cylinder, new Vector3(side * 0.26f, 0.70f, -0.38f),
                    new Vector3(side * 0.40f, 0.70f, -0.38f), 0.026f);
                Vector3 mirror = new Vector3(side * 0.34f, 0.87f, -0.26f);
                steering.Rod(steel, _cylinder, new Vector3(side * 0.25f, 0.70f, -0.35f), mirror, 0.011f);
                steering.Add(steel, _sphere, mirror, Quaternion.identity, new Vector3(0.18f, 0.092f, 0.034f));
            }
            steering.Add(paint, _sphere, new Vector3(0f, 0.305f, 0.015f), Quaternion.identity, new Vector3(0.19f, 0.13f, 0.61f));
            steering.Add(rubber, _sphere, new Vector3(0f, 0.49f, -0.09f), Quaternion.identity, new Vector3(0.275f, 0.245f, 0.15f));
            steering.Add(glass, _sphere, new Vector3(0f, 0.49f, -0.015f), Quaternion.identity, new Vector3(0.218f, 0.203f, 0.056f));
            steering.Emit(fork, "Handlebar and headlamp");

            MeshParts wheel = new MeshParts(this);
            Quaternion sideView = Quaternion.Euler(0f, 90f, 0f);
            wheel.Ring(rubber, 0.307f, 0.307f, 0.053f, 32, sideView);
            wheel.Ring(steel, 0.259f, 0.259f, 0.017f, 28, sideView);
            for (int spoke = 0; spoke < 5; spoke++)
            {
                float angle = spoke * Mathf.PI * 2f / 5f;
                wheel.Rod(steel, _cylinder, Vector3.zero, new Vector3(0f, Mathf.Cos(angle), Mathf.Sin(angle)) * 0.26f, 0.018f);
            }
            wheel.Rod(steel, _cylinder, Vector3.left * 0.105f, Vector3.right * 0.105f, 0.067f);
            var wheelMeshes = wheel.CreateMeshes("Motorcycle tire");
            Transform frontWheel = Child(fork, "Front wheel", Vector3.zero);
            Transform rearWheel = Child(visual, "Rear wheel", rear);
            MeshParts.AddRenderers(frontWheel, wheelMeshes, "Motorcycle tire");
            MeshParts.AddRenderers(rearWheel, wheelMeshes, "Motorcycle tire");

            AudioSource engine = motor.gameObject.AddComponent<AudioSource>();
            engine.clip = engineLoop;
            engine.loop = true;
            engine.playOnAwake = false;
            engine.spatialBlend = 0.8f;
            engine.minDistance = 1.5f;
            engine.maxDistance = 24f;
            engine.volume = 0.14f;
            motor.gameObject.AddComponent<BicycleController>().ConfigureMotorcycle(visual, fork, frontWheel, rearWheel, engine);
            motor.gameObject.AddComponent<InteractableTrigger>()
                .Configure(InteractableKind.Motorcycle, triggerId, "ui.btn.ride_motorcycle", 2.7f);
        }

        private AudioClip CreateMotorcycleEngineLoop()
        {
            const int sampleRate = 22050;
            var samples = new float[sampleRate];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = i / (float)sampleRate * 2f * Mathf.PI;
                float pulse = 0.49f + 0.31f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(19f * t), 3f) +
                    0.19f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(19f * t + 2.16f), 3f);
                samples[i] = pulse * (0.32f * Mathf.Sin(57f * t) + 0.13f * Mathf.Sin(95f * t) + 0.07f * Mathf.Sin(152f * t));
            }
            AudioClip clip = AudioClip.Create("Selman motorcycle twin engine", sampleRate, 1, sampleRate, false);
            clip.SetData(samples, 0);
            _ownedAssets.Add(clip);
            return clip;
        }
    }
}
