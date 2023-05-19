using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Damageable : Touchable
{
    public delegate bool OnDamaged(int damageReceived);
	public delegate void OnHeal(int healingReceived);

	public OnDamaged Damaged;
	public OnHeal Healed;
}
