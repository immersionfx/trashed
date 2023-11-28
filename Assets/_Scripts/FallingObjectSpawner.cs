using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine;
using Niantic.Lightship.AR.Semantics;
using UnityEngine.XR.ARFoundation;
using FrostweepGames.Plugins.GoogleCloud.SpeechRecognition.Examples;

[System.Serializable]
public struct ItemStruct
{ 
    public string name;
    public GameObject objPrefab;    
    public Material objMaterial;
    public AudioClip objClip;
    public int limit;
    public string objDescription;
}

public class FallingObjectSpawner : MonoBehaviour
{
    //Events
    public static event Action hideRecorder;

    [Header("Managers")]
    [SerializeField] private AudioManager _audioManager;
    [SerializeField] private AROcclusionManager _occlusionManager;
    [SerializeField] private ARSemanticSegmentationManager _semanticManager;

    [Header("Indicators")]
    [SerializeField] private int counter = 0;
    [SerializeField] private float _FREQUENCY = 0.2f;
    [SerializeField] private float _MESHQUALITY = 0.5f;
    [SerializeField] private float _HEIGHOFFSET = 25f;
    [SerializeField] private float _FALLINGDURATION = 5f;
    [SerializeField] private bool isFallingObjects = false;
    [SerializeField] private bool isCombining = false;
    [SerializeField] private bool simplifyMesh = false;

    [Header("UI Elements")]
    [SerializeField] private GameObject infoCanvas;
    [SerializeField] private TMP_Text infoCanvasTxt;
    [SerializeField] private GameObject counterMessage;
    [SerializeField] private TMP_Text countText;

    [Header("Prefabs")]
    [SerializeField] private ItemStruct[] objectPrefabs; // object prefabs
    private GameObject fallingObjectPrefab; // Reference to the object prefab  

    [Tooltip("Set this to -1 for non-testing mode")]
    [SerializeField] private int selectedId;
    
    private AudioSource myAudio;

    [Header("Lists")]    
    [SerializeField] private List<MeshFilter> sourceMeshFilters;
    private List<GameObject> combinedMeshes;


    void Start()
    {        
        _occlusionManager.enabled = false;

        //Mesh mesh = fallingObjectPrefab.GetComponent<MeshFilter>().sharedMesh;
        //verticesCount = mesh.vertexCount;

        myAudio = GetComponent<AudioSource>();

        infoCanvas.SetActive(false);

        //Events
        AudioManager.startSpawner += spawnItem;
        AudioManager.stopSpawner += stopSpawning;
        AudioManager.restartSpawner += restartSpawning;
    }


    //Called from AudioManager
    //Starts spawning
    public void spawnItem(int itemId)
    {
        sourceMeshFilters = new List<MeshFilter>();
        combinedMeshes = new List<GameObject>();

        if (selectedId == -1) selectedId = itemId;
        fallingObjectPrefab = objectPrefabs[selectedId].objPrefab;  

        //position the spawner 25m above the camera
        transform.position = Camera.main.gameObject.transform.position + Vector3.up * _HEIGHOFFSET; 

        // Start spawning the falling objects around the user
        StartCoroutine(SpawnFallingObject(objectPrefabs[selectedId].limit));        
        StartCoroutine(PlayAudioClip(_FALLINGDURATION));

        //Disable Semantic Segmentation
        _semanticManager.enabled = false;
        
        //Enable Occlusion
        _occlusionManager.enabled = true;

        //Show info  
        StartCoroutine(showInfoCanvas());        
    }


    IEnumerator showInfoCanvas()
    {
        yield return new WaitForSeconds(1f);
        infoCanvas.SetActive(true);
        infoCanvas.transform.position = Camera.main.transform.position + Camera.main.transform.forward * 2f;
        infoCanvasTxt.text = objectPrefabs[selectedId].objDescription.Replace("||", "\n");
    }

    IEnumerator SpawnFallingObject(int limit)
    {
        isFallingObjects = true;
        counter = 0;
        counterMessage.SetActive(true);

        while (isFallingObjects && counter < limit)
        {
            InstantiateFallingObjects(5, limit);
            countText.text = counter.ToString() + " " + objectPrefabs[selectedId].name;
            yield return new WaitForSeconds(_FREQUENCY);
        }
        
        yield return new WaitForSeconds(_FALLINGDURATION * 2);        
        myAudio.Stop();
        CombineAndSimplifyMeshes();        
        yield return new WaitForSeconds(_FALLINGDURATION);
        CombineAndSimplifyMeshes();
    }

    void InstantiateFallingObjects(int numOfObjects, int limit)
    {
        for (int i = 0; i < numOfObjects; i++)
        {            
            GameObject fallingObject = Instantiate(fallingObjectPrefab);
            sourceMeshFilters.Add(fallingObject.GetComponent<MeshFilter>());
            fallingObject.transform.position = new Vector3(UnityEngine.Random.Range(-5f, 5f), transform.position.y, UnityEngine.Random.Range(-5f, 5f));
            counter++;

            //Time to combine/simplify?
            if (counter % 300 == 0 && counter < (limit-300) && !isCombining) { print("@@@StartCombine"); StartCoroutine(CombineMeshes()); }
            //if (counter % 300 == 0 && !isCombining) CombineAndSimplifyMeshes();
        }
    }
        

    IEnumerator PlayAudioClip(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        myAudio.clip = objectPrefabs[selectedId].objClip;
        myAudio.Play();
    }

    IEnumerator CombineMeshes()
    {
        List<CombineInstance> combineList = new List<CombineInstance>();
        int found = 0;
        isCombining = true;

        for (int i = sourceMeshFilters.Count - 1; i >= 0; i--)
        {
            if (sourceMeshFilters[i].gameObject.GetComponent<Rigidbody>() != null) continue;
            combineList.Add(new CombineInstance() { mesh = sourceMeshFilters[i].sharedMesh, transform = sourceMeshFilters[i].transform.localToWorldMatrix });
            Destroy(sourceMeshFilters[i].gameObject);
            sourceMeshFilters.RemoveAt(i);
            found++;
            yield return null;            
        }

        Debug.LogFormat("YIELD: Found {0} items", found);

        var mesh = new Mesh
        {
            //The number of vertices in the combined mesh exceeds the maximum supported vertex count (65535) of the UInt16 index format. 
            //Consider using the UInt32 IndexFormat for the combined mesh to increase the max vertex count.
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };

        var combineArray = combineList.ToArray();
        mesh.CombineMeshes(combineArray);

        // Now, Create a new GameObject, add a MeshFilter component to the new GameObject
        GameObject newObject = new GameObject("MeshObject");
        newObject.AddComponent<MeshFilter>();
        newObject.AddComponent<MeshRenderer>();

        MeshFilter newMeshFilterObject = newObject.GetComponent<MeshFilter>();
        newMeshFilterObject.mesh = mesh;
        newObject.GetComponent<Renderer>().material = objectPrefabs[selectedId].objMaterial;

        //Add MeshCollider
        newObject.AddComponent<MeshCollider>();

        //Add it to combinedMeshes List
        combinedMeshes.Add(newObject);
        isCombining = false;
        print("@@@ END Combine");
    }

    private void CombineAndSimplifyMeshes()
    {   
        List<CombineInstance> combineList = new List<CombineInstance>();
        int found = 0;

        for (int i = sourceMeshFilters.Count - 1; i >= 0; i--)
        {
            if (sourceMeshFilters[i].gameObject.GetComponent<Rigidbody>() != null) continue;
            combineList.Add(new CombineInstance() { mesh = sourceMeshFilters[i].sharedMesh, transform = sourceMeshFilters[i].transform.localToWorldMatrix });
            Destroy(sourceMeshFilters[i].gameObject);
            sourceMeshFilters.RemoveAt(i);
            found++;
        }

        Debug.LogFormat("Found {0} items", found);

        var mesh = new Mesh
        {
            //The number of vertices in the combined mesh exceeds the maximum supported vertex count (65535) of the UInt16 index format. 
            //Consider using the UInt32 IndexFormat for the combined mesh to increase the max vertex count.
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };

        var combineArray = combineList.ToArray();
        mesh.CombineMeshes(combineArray);

        // Now, Create a new GameObject, add a MeshFilter component to the new GameObject
        GameObject newObject = new GameObject("MeshObject");
        newObject.AddComponent<MeshFilter>();
        newObject.AddComponent<MeshRenderer>();

        MeshFilter newMeshFilterObject = newObject.GetComponent<MeshFilter>();
        newMeshFilterObject.mesh = mesh; 
        newObject.GetComponent<Renderer>().material = objectPrefabs[selectedId].objMaterial;

        //Simplify?
        if (simplifyMesh) 
        {
            var originalMesh = newMeshFilterObject.sharedMesh;
            var meshSimplifier = new UnityMeshSimplifier.MeshSimplifier();
            meshSimplifier.Initialize(originalMesh);
            meshSimplifier.SimplifyMesh(_MESHQUALITY);
            var destMesh = meshSimplifier.ToMesh();
            newMeshFilterObject.sharedMesh = destMesh;
        }

        //Add MeshCollider
        //newObject.AddComponent<MeshCollider>();
        
        //Add it to combinedMeshes List
        combinedMeshes.Add(newObject);
    }

    public void removeAll()
    {
        foreach (MeshFilter mesh in sourceMeshFilters)
            Destroy(mesh.gameObject);

        foreach (GameObject mesh in combinedMeshes)
            Destroy(mesh);
    }

    void OnDisable()
    {
        AudioManager.startSpawner -= spawnItem;
        AudioManager.stopSpawner -= stopSpawning;
        AudioManager.restartSpawner -= restartSpawning;
        StopAllCoroutines();
    }

    void OnDestroy()
    {
        isFallingObjects = false;
        sourceMeshFilters = new List<MeshFilter>();
        StopAllCoroutines();
    }

    //Called from AudioManager when asked to Stop
    public void stopSpawning()
    {
        Debug.Log("@@@ stopSpawning");
        isFallingObjects = false;
    }

    //Called from AudioManager when asked to restart
    public void restartSpawning()
    {
        Debug.Log("@@@ restartSpawning");

        isFallingObjects = false;
        counterMessage.SetActive(false);
        infoCanvas.SetActive(false);
        countText.text = string.Empty;
        hideRecorder?.Invoke(); //@AudioManager        

        removeAll();

        SceneManager.LoadScene("Start");

        // Enable/Disable Managers
        // _occlusionManager.enabled = false;
        // _semanticManager.enabled = true;
    }

#if UNITY_EDITOR

    //Tests
    void Update()
    {
        if (Input.GetKeyUp(KeyCode.Space))
        {
            isFallingObjects = false;
            myAudio.Stop();
            removeAll();
        }
        if (Input.GetKeyUp(KeyCode.C))
        {
            CombineAndSimplifyMeshes();
        }
        if (Input.GetKeyUp(KeyCode.B)) //spawn
        {
            spawnItem(UnityEngine.Random.Range(0,4));
        }
        if (Input.GetKeyUp(KeyCode.N)) //stop
        {
            stopSpawning();
        }
        if (Input.GetKeyUp(KeyCode.M)) //restart
        {
            restartSpawning();
        }
    }
#endif    
}
