using UnityEngine;

// Linhas comentadas neste arquivo são resquícios de outro código que
// estava testando. Caso vá implementar combate, por favor use as
// variáveis e outros elementos comentados aqui para manter o padrão.

[RequireComponent(typeof(FighterMotor))]
//[RequireComponent(typeof(FighterCombat))]
public class PlayerKeyboardInput : MonoBehaviour
{
    private FighterMotor motor;
    //private FighterCombat combat;

    private void Awake()
    {
        motor = GetComponent<FighterMotor>();
        //combat = GetComponent<FighterCombat>();
    }

    private void Update()
    {
        float horizontal = 0f;

        if (Input.GetKey(KeyCode.LeftArrow))
            horizontal -= 1f;

        if (Input.GetKey(KeyCode.RightArrow))
            horizontal += 1f;

        float vertical = 0f;

        if (motor.IsFlying)
        {
            if (Input.GetKey(KeyCode.UpArrow))
                vertical += 1f;

            if (Input.GetKey(KeyCode.DownArrow))
                vertical -= 1f;
        }
        else if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            motor.RequestJump();
        }

        motor.SetMoveInput(new Vector2(horizontal, vertical));

        if (Input.GetKeyDown(KeyCode.Z))
            motor.ToggleFlying();

        if (Input.GetKeyDown(KeyCode.X))
            motor.TryTeleport();

        //if (Input.GetKeyDown(KeyCode.C))
        //    combat.TryPunch();

        //if (Input.GetKeyDown(KeyCode.V))
        //    combat.TryFireEnergyBall();
    }
}