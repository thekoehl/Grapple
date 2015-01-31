using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Link
{
    public Transform transform;
    public Rigidbody rigidBody;
    public CharacterJoint joint;

    public bool isKinematic { get { return rigidBody.isKinematic; } set { rigidBody.isKinematic = value; } }
    public bool activeSelf { get { return transform.gameObject.activeSelf; } }

    public void SetActive(bool _b)
    {
        transform.gameObject.SetActive(_b);
    }
}

public class Grapple : MonoBehaviour
{
    public delegate void GrappleAction();

    public float grappleSpeed = 5;
    public float maxExtendedLength = 100;
    public float linkSpacing = 1;
    public float linkScale = 1;
    public bool alternateJoints = false;
    public GameObject linkPrefab = null;
    public bool IsExtended { get { return extended; } }

    private bool extended = false;
    // Called when grapple makes contact with a rigidbody
    public GrappleAction OnGrappleContact;
    // Called when grapple fails to make contact and is fully extended
    public GrappleAction OnGrappleFail;
    // Called when grapple is disconnected from player
    public GrappleAction OnGrappleDisconnect;
    // Called when grapple is attached to player
    public GrappleAction OnGrappleConnected;

    private List<Link> linkBuffer = new List<Link>();
    private int activeJoints = 0;
    private float maxRetractLength = 0;

    private Rigidbody connectedPlayer = null;
    private CharacterJoint playerJoint = null;

    void OnDrawGizmos()
    {
        Gizmos.DrawRay(transform.position, transform.forward * maxExtendedLength);
    }

    void Start()
    {
        linkSpacing = Mathf.Max(linkSpacing, 0.001f);
        int maxJointCount = Mathf.CeilToInt((maxExtendedLength / linkSpacing) * 1.1f);
        for (int i = 0; i < maxJointCount; i++)
        {
            GameObject link = Instantiate<GameObject>(linkPrefab);
            link.transform.localScale = Vector3.one * linkScale;
            //link.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector;
   
            link.SetActive(false);

            linkBuffer.Add(new Link() 
            {
                transform = link.transform,
                rigidBody = link.AddComponent<Rigidbody>(),
                joint = link.AddComponent<CharacterJoint>() 
            });

            Debug.DrawRay(transform.position + (transform.forward * linkSpacing * i), transform.up, Color.blue, 0.2f);
        }

        Debug.DrawRay(transform.position, transform.forward * maxExtendedLength, Color.red, 0.2f);
    }
    
    void SetJointsKinematic(bool _value)
    {
        for (int i = 0; i < activeJoints; i++)
            linkBuffer[i].isKinematic = _value;
    }

    IEnumerator iAutoRetract(float _speed, float _maxRetractLength)
    {
        maxRetractLength = _maxRetractLength;
        while (extended)
        {
            Retract(_speed);
            yield return 0;
        }
    }

    IEnumerator iRetract(float _speed)
    {
        //SetJointsKinematic(true);
        Rigidbody connBody = playerJoint.connectedBody;
        playerJoint.connectedBody = null;

        connBody.transform.position = Vector3.MoveTowards(connBody.transform.position, connectedPlayer.transform.position, Time.deltaTime * _speed);

        if (Vector3.SqrMagnitude(connectedPlayer.transform.position - connBody.transform.position) < 0.01f)
        {
            linkBuffer[activeJoints - 1].SetActive(false);
            activeJoints--;

            if (activeJoints <= 0)
                yield break;

            playerJoint.connectedBody = linkBuffer[activeJoints - 1].rigidBody;
        }
        else
        {
            playerJoint.connectedBody = connBody;
        }

        yield return 0;

        //SetJointsKinematic(false);
    }

    IEnumerator iFire()
    {
        Vector3 direction = transform.forward;
        Vector3 spacing = direction * linkSpacing;
        Vector3 curPosition = transform.position;

        RaycastHit rHit;
        Vector3 finalDestination;

        do
        {
            finalDestination = transform.position + (transform.forward * maxExtendedLength); //willHit ? rHit.point : transform.position + (direction * maxRopeLength);
            curPosition = Vector3.MoveTowards(curPosition, finalDestination, Time.deltaTime * grappleSpeed);
            direction = (curPosition - transform.position).normalized;
            spacing = direction * linkSpacing;

            int jointCount = Mathf.CeilToInt(Vector3.Distance(curPosition, transform.position) / linkSpacing);
            activeJoints = 0;

            for (int i = 0; i < jointCount - 1; i++)
            {
                linkBuffer[i].SetActive(true);
                activeJoints++;

                linkBuffer[i].isKinematic = true;
                linkBuffer[i].transform.position = (curPosition - (spacing * i));
                linkBuffer[i].transform.localScale = Vector3.one * linkScale;

                if (alternateJoints && i % 2 == 0)
                    linkBuffer[i].transform.rotation = Quaternion.FromToRotation(Vector3.forward, direction) * Quaternion.Euler(0, 0, 90);
                else
                    linkBuffer[i].transform.rotation = Quaternion.FromToRotation(Vector3.forward, direction);
            }

            float rayDist = linkSpacing * grappleSpeed * Time.deltaTime;
            Debug.DrawRay(curPosition, direction * rayDist, Color.red);
            if (Physics.Raycast(curPosition, direction * rayDist, out rHit, linkSpacing))
            {
                LinkRope();

                Rigidbody hitRigidbody = rHit.collider.gameObject.GetComponent<Rigidbody>();
                if (hitRigidbody == null)
                {
                    hitRigidbody = rHit.collider.gameObject.AddComponent<Rigidbody>();
                    hitRigidbody.isKinematic = true;
                }

                if (linkBuffer[0].joint == null)
                    linkBuffer[0].joint = linkBuffer[0].rigidBody.gameObject.AddComponent<CharacterJoint>();

                linkBuffer[0].joint.connectedBody = hitRigidbody;

                if (OnGrappleContact != null)
                    OnGrappleContact.Invoke();

                yield break;
            }
            
            yield return 0;
        }
        while (Vector3.SqrMagnitude(finalDestination - curPosition) > 0.01f);

        LinkRope();
        DestroyImmediate(linkBuffer[0].joint);

        // Failed to latch on to any object
        if (OnGrappleFail != null)
            OnGrappleFail.Invoke();
    }

    void LinkRope()
    {
        extended = true;
        Rigidbody prevRigidbody = null;
        for(int i = 0; i < linkBuffer.Count; i++)
        {
            if(!linkBuffer[i].activeSelf)
                continue;

            linkBuffer[i].isKinematic = false;
            
            if (prevRigidbody != null)
            {
                if (linkBuffer[i].joint == null)
                    linkBuffer[i].joint = linkBuffer[i].rigidBody.gameObject.AddComponent<CharacterJoint>();

                linkBuffer[i].joint.connectedBody = prevRigidbody;
            }

            if (linkBuffer[i].joint != null)
            {
                linkBuffer[i].joint.axis = new Vector3(0, 0, 1);
                linkBuffer[i].joint.swing1Limit = new SoftJointLimit() { limit = 90, damper = 1 };
                linkBuffer[i].joint.swing2Limit = new SoftJointLimit() { limit = 90, damper = 1 };
            }

            prevRigidbody = linkBuffer[i].rigidBody;
        }
    }

    public void DisconnectFromTarget()
    {
        linkBuffer[0].joint.connectedBody = null;
        DestroyImmediate(linkBuffer[0].joint);
    }

    public void ConnectPlayer(Rigidbody _body)
    {
        connectedPlayer = _body;
        playerJoint = _body.gameObject.AddComponent<CharacterJoint>();
        playerJoint.connectedBody = linkBuffer[activeJoints - 1].rigidBody;

        playerJoint.axis = new Vector3(0, 0, 1);
        playerJoint.swing1Limit = new SoftJointLimit() { limit = 180, damper = 1 };
        playerJoint.swing2Limit = new SoftJointLimit() { limit = 180, damper = 1 };
        playerJoint.lowTwistLimit = new SoftJointLimit() { limit = -180, damper = 1 };
        playerJoint.highTwistLimit = new SoftJointLimit() { limit = 180, damper = 1 };

        if (OnGrappleConnected != null)
            OnGrappleConnected.Invoke();
    }

    public void DisconnectPlayer(Rigidbody _body)
    {
        DestroyImmediate(playerJoint);

        playerJoint = null;
        connectedPlayer = null;

        if (OnGrappleDisconnect != null)
            OnGrappleDisconnect.Invoke();
    }

    public void ResetLinks()
    {
        if (linkBuffer[0].joint != null)
            linkBuffer[0].joint.connectedBody = null;

        if (connectedPlayer != null)
            DisconnectPlayer(connectedPlayer);

        for (int i = 0; i < linkBuffer.Count; i++)
        {
            //DestroyImmediate(linkBuffer[i].joint);
            if(linkBuffer[i].joint != null)
                linkBuffer[i].joint.connectedBody = null;

            linkBuffer[i].rigidBody.isKinematic = true;
            linkBuffer[i].SetActive(false);
        }

        activeJoints = 0;
        extended = false;
    }

    public void Fire()
    {
#if UNITY_5
        StopAllCoroutines();
        ResetLinks();

        StartCoroutine(iFire());
#else
        StopAllCoroutines();
        ResetLinks();

        StartCoroutine("iFire");
#endif

    }

    public void Retract(float _speed)
    {
        if(maxRetractLength > 0 && Vector3.SqrMagnitude(linkBuffer[0].transform.position - transform.position) < (maxRetractLength * maxRetractLength))
            return;

        StartCoroutine(iRetract(_speed));
        //connectedPlayer.isKinematic = false;

        if (activeJoints == 0)
            ResetLinks();
    }

    public void AutoRetract(float _speed, float _maxRetractLength)
    {
        StopAllCoroutines();
        StartCoroutine(iAutoRetract(_speed, _maxRetractLength));
    }
}
