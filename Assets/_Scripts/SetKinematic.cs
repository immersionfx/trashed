
using UnityEngine;
using System.Collections;
using System.Threading;


public class SetKinematic : MonoBehaviour
{
    private Rigidbody rb;


    IEnumerator Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.angularVelocity = new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), Random.Range(-5f, 5f));

        yield return new WaitForSeconds(1f);

        StartCoroutine(checkIfKinematic());
        //StartCoroutine(checkIfGrounded());
    }

    // IEnumerator checkIfGrounded()
    // {
    //     while (rb.velocity.y > 0)
    //     {
    //         yield return new WaitForSeconds(.3f);
    //     }        
    // }

    IEnumerator checkIfKinematic()
    {
        yield return new WaitForSeconds(3f);

        while (rb.velocity.magnitude > 0.02f)
        {
            yield return new WaitForSeconds(.3f);
        }

        // Disable further physics interactions
        Destroy(rb);
    }    
}
