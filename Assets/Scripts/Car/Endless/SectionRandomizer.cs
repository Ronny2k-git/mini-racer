using UnityEngine;

// Shows one random child of every slot each time the section is enabled,
// so pooled sections look different every time they come back on the road
public class SectionRandomizer : MonoBehaviour
{
    [SerializeField]
    Transform[] slots;

    void OnEnable()
    {
        foreach (Transform slot in slots)
        {
            if (slot == null || slot.childCount == 0) continue;

            int chosen = Random.Range(0, slot.childCount);

            for (int i = 0; i < slot.childCount; i++)
                slot.GetChild(i).gameObject.SetActive(i == chosen);
        }
    }
}
