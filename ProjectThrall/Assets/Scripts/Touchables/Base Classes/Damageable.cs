using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Damageable : Moveable
{
   public delegate bool OnDamaged(int damageReceived);
	public delegate void OnHeal(int healingReceived);
	public delegate void OnInflict(Keyword infliction);

	public OnDamaged Damaged;
	public OnHeal Healed;
	public OnInflict Inflicted;
}
