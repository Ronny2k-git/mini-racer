using UnityEngine;

public class CarHandler : MonoBehaviour
{
    [SerializeField]
    Rigidbody rb;

    [SerializeField]
    Transform gameModel;

    //Max Values
    float maxSteerVelocity = 2;
    float maxForwardVelocity = 30;

    // Multipliers
    float accelerationMultiplier = 3;
    float breaksMultiplier = 15;
    float steeringMUltiplier = 5;


    // Input
    Vector2 input = Vector2.zero;

    // Start is called before the firts frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        // Rotate car model when "turning"
        gameModel.transform.rotation = Quaternion.Euler(0, rb.linearVelocity.x * 5, 0);
    }

    // FixedUpdate is called in fixed intervals
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

        if (rb.linearVelocity.z <= 0)
            rb.linearVelocity = Vector3.zero;
    }

    void Accelerate()
    {
        rb.linearDamping = 0;

        // Stay within the speed limit
        if (rb.linearVelocity.z >= maxForwardVelocity)
            return;

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
            // Move the car sideways
            float speedBaseSteerLimit = rb.linearVelocity.z / 5.0f;
            speedBaseSteerLimit = Mathf.Clamp01(speedBaseSteerLimit);

            rb.AddForce(rb.transform.right * steeringMUltiplier * input.x * speedBaseSteerLimit);

            // Normalize the X Velocity
            float normalizedX = rb.linearVelocity.x / maxSteerVelocity;

            // Ensure that we don't allow it to get bigger than 1 in magnitude
            normalizedX = Mathf.Clamp(normalizedX, -1.0f, 1.0f);

            // Make sure we stay within the turn speed limit
            rb.linearVelocity = new Vector3(normalizedX * maxSteerVelocity, 0, rb.linearVelocity.z);
        }
        else
        {
            // Auto center car
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, new Vector3(0, 0, rb.linearVelocity.z), Time.fixedDeltaTime * 3);
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



