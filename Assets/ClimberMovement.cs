using UnityEngine;
using UnityEngine.AI;

public class ClimberMovement : MonoBehaviour
{
    private Transform finalPos;
    private NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        GameObject target = GameObject.Find("FinalDestination");
       
        finalPos = target.transform;
       
    }

    void Update()
    {

         agent.SetDestination(finalPos.position);

    }
}
