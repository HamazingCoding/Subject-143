using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>
    /// Nightreign spiritstream / spirit spring: stand in it and press Jump to be launched high into the air.
    /// You can steer while airborne and land without fall damage.
    /// </summary>
    public class SpiritSpring : MonoBehaviour
    {
        public float radius = 1.4f;
        public float launchHeight = 14f;
        public Transform ring;

        NightfarerCharacter[] characters = new NightfarerCharacter[0];
        float refresh;

        void Update()
        {
            if (ring != null)
            {
                ring.Rotate(0f, 90f * Time.deltaTime, 0f, Space.World);
                ring.localPosition = new Vector3(0f, 0.25f + Mathf.Sin(Time.time * 2f) * 0.1f, 0f);
            }
            refresh -= Time.deltaTime;
            if (refresh <= 0f)
            {
                refresh = 1f;
                characters = FindObjectsByType<NightfarerCharacter>(FindObjectsSortMode.None);
            }
            foreach (var c in characters)
            {
                if (c == null) continue;
                Vector3 d = c.transform.position - transform.position;
                bool inside = new Vector2(d.x, d.z).magnitude <= radius && Mathf.Abs(d.y) < 1.5f;
                if (inside) c.ActiveSpring = this;
                else if (c.ActiveSpring == this) c.ActiveSpring = null;
            }
        }
    }
}
