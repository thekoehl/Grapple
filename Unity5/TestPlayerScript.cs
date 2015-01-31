using UnityEngine;
using System.Collections;

public class TestPlayerScript : MonoBehaviour 
{
    public Grapple grapple;
    public Rigidbody playersRigidbody;

    void OnEnable()
    {
        if (playersRigidbody == null || grapple == null)
            return;

        grapple.OnGrappleContact = OnGrappleContact;
        grapple.OnGrappleFail = OnGrappleFail;
        grapple.OnGrappleConnected = OnGrappleConnected;
        grapple.OnGrappleDisconnect = OnGrappleDisconnected;
    }

    void OnDisable()
    {
        if (grapple == null)
            return;

        grapple.OnGrappleContact = null;
        grapple.OnGrappleFail = null;
        grapple.OnGrappleConnected = null;
        grapple.OnGrappleDisconnect = null;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            grapple.Fire();

        if (grapple.IsExtended && Input.GetKey(KeyCode.UpArrow))
            grapple.Retract(grapple.grappleSpeed * 0.5f);

        if (grapple.IsExtended && Input.GetMouseButtonDown(1))
        {
            grapple.DisconnectFromTarget();
            grapple.AutoRetract(grapple.grappleSpeed, -1);
        }
    }

    void OnGrappleContact() 
    {
        Debug.Log("Contact");

        grapple.ConnectPlayer(playersRigidbody);
        playersRigidbody.isKinematic = false;

        grapple.AutoRetract(grapple.grappleSpeed, 5);
    }

    void OnGrappleFail()
    {
        Debug.Log("Failed");
        grapple.ConnectPlayer(playersRigidbody);

        grapple.AutoRetract(grapple.grappleSpeed, -1);
    }

    void OnGrappleConnected()
    {
        Debug.Log("Connected");
    }

    void OnGrappleDisconnected()
    {
        Debug.Log("Disconnected");
    }
}
