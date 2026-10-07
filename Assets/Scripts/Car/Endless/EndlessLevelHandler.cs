using System.Collections;
using UnityEngine;

public class EndlessLevelHandler : MonoBehaviour
{
    [SerializeField]
    GameObject[] sectionsPreFabs;

    GameObject[] sectionsPool = new GameObject[20];

    GameObject[] sections = new GameObject[10];

    Transform playerCardTransform;

    WaitForSeconds waitFor100ms = new WaitForSeconds(0.1f);

    const float sectionLength = 26;

    // Shared X position so every section lines up, regardless of each prefab's root offset
    float sectionXPosition;

    // Start is called once before the first execution of Update
    void Start()
    {
        playerCardTransform = GameObject.FindGameObjectWithTag("Player").transform;

        sectionXPosition = sectionsPreFabs[0].transform.position.x;

        int prefabIndex = 0;

        // Create a pool for endless sections
        for (int i = 0; i < sectionsPool.Length; i++)
        {
            sectionsPool[i] = Instantiate(sectionsPreFabs[prefabIndex]);
            sectionsPool[i].SetActive(false);

            prefabIndex++;

            // Loop the prefab index if we run out of prefabs
            if (prefabIndex > sectionsPreFabs.Length - 1)
                prefabIndex = 0;
        }

        // Add the first section to the road
        for (int i = 0; i < sections.Length; i++)
        {
            // Get a random section
            GameObject randomSection = GetRandomSectionFromPool();

            // Move it into position and set it to active
            randomSection.transform.position = new Vector3(sectionXPosition, 0, i * sectionLength);
            randomSection.SetActive(true);

            // Set the section in the array
            sections[i] = randomSection;
        }

        StartCoroutine(UpdateLessOftenCO());
    }

    IEnumerator UpdateLessOftenCO()
    {
        while (true)
        {
            UpdateSectionPositions();

            yield return waitFor100ms;
        }
    }

    void UpdateSectionPositions()
    {
        for (int i = 0; i < sections.Length; i++)
        {
            // Check if section is too far befind
            if (sections[i].transform.position.z - playerCardTransform.position.z < -sectionLength)
            {
                // Store the position of the section and disable it
                Vector3 lastSectionPosition = sections[i].transform.position;
                sections[i].SetActive(false);

                // Get bew sectuib & enable it & move it forward
                sections[i] = GetRandomSectionFromPool();

                //  Move the bew section into place and active it
                sections[i].transform.position = new Vector3(sectionXPosition, 0, lastSectionPosition.z + sectionLength * sections.Length);
                sections[i].SetActive(true);
            }
        }
    }

    GameObject GetRandomSectionFromPool()
    {
        //Pick a random index anh hope that is is available
        int randomIndex = Random.Range(0, sectionsPool.Length);

        bool isNewSectionFound = false;

        while (!isNewSectionFound)
        {
            // Check if the section is not active, in that case we've found a section
            if (!sectionsPool[randomIndex].activeInHierarchy)
                isNewSectionFound = true;
            else
            {
                // If it was active, try to find another one to increase the index
                randomIndex++;

                // Ensure to loop around if the end of the array is reached
                if (randomIndex > sectionsPool.Length - 1)
                    randomIndex = 0;
            }
        }

        return sectionsPool[randomIndex];
    }
}

