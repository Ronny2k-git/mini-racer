using UnityEngine;

public class CarHandler : MonoBehaviour
{
    [SerializeField]
    Rigidbody rb;

    // Multipliers
    float accelerationMultiplier = 3;
    float breaksMultiplier = 15;
    float steeringMUltiplier = 5;

    // Input
    Vector2 input = Vector2.zero;

    void Start()
    {

    }

    void Update()
    {

    }

    private void FixedUpdate()
    {
        // Apply Acceleration
        if (input.y > 0)
            Accelerate();

        else
            rb.linearDamping = 0.2f;

        if (input.y < 0)
            Brake();

        Steer();
    }

    void Accelerate()
    {
        rb.linearDamping = 0;

        rb.AddForce(rb.transform.forward * accelerationMultiplier * input.y);
    }

    void Brake()
    {
        // Only breeak if we are going forward
        if (rb.linearVelocity.z <= 0) return;

        rb.AddForce(rb.transform.forward * breaksMultiplier * input.y);

    }

    void Steer()
    {
        if (Mathf.Abs(input.x) > 0)
        {
            rb.AddForce(rb.transform.right * steeringMUltiplier * input.x);
        }
    }

    // MIght be usefull in the future, but it's not necessary for endless driving
    void Reverse()
    {
        // rb.AddForce(-transform.forward * accelerationMultiplier);
    }

    public void SetInput(Vector2 inputVector)
    {
        inputVector.Normalize();

        input = inputVector;
    }
}



