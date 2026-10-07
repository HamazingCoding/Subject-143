using UnityEngine;

namespace Subject143.Nightfarer
{
    /// <summary>World-space floating damage number.</summary>
    public class DamageNumber : MonoBehaviour
    {
        float life;
        TextMesh text;

        public static void Spawn(Vector3 position, float amount, bool big)
        {
            var go = new GameObject("DamageNumber");
            go.transform.position = position;
            var dn = go.AddComponent<DamageNumber>();
            dn.text = go.AddComponent<TextMesh>();
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            dn.text.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            dn.text.text = Mathf.RoundToInt(amount).ToString();
            dn.text.characterSize = big ? 0.09f : 0.065f;
            dn.text.fontSize = 48;
            dn.text.anchor = TextAnchor.MiddleCenter;
            dn.text.color = big ? new Color(1f, 0.45f, 0.2f) : new Color(1f, 0.92f, 0.6f);
        }

        void Update()
        {
            life += Time.deltaTime;
            transform.position += Vector3.up * (1.2f * Time.deltaTime);
            var cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            if (life > 0.9f) Destroy(gameObject);
        }
    }
}
