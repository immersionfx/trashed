using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UILookAt : MonoBehaviour
{

    void LateUpdate()
    {        
        if (!Camera.main) return;
        var lookPos = transform.position - Camera.main.transform.position;
        lookPos.y = 0;
        transform.rotation = Quaternion.LookRotation(lookPos);
    }
}
