using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;

public class CombatSystemEditModeTest
{
    [Test]
    public void CombatSystemCanInitialize()
    {
        // Arrange
        var combatSystem = new GameObject("CombatSystem").AddComponent<CombatSystem>();
        
        // Act
        combatSystem.Initialize();
        
        // Assert
        Assert.That(combatSystem.IsInitialized, Is.True);
        
        // Cleanup
        Object.DestroyImmediate(combatSystem.gameObject);
    }
    
    [Test]
    public void CombatSystemCanAttack()
    {
        // Arrange
        var combatSystem = new GameObject("CombatSystem").AddComponent<CombatSystem>();
        combatSystem.Initialize();
        var target = new GameObject("Target").AddComponent<Damageable>();
        
        // Act
        combatSystem.Attack(target);
        
        // Assert
        Assert.That(target.CurrentHealth, Is.LessThan(target.MaxHealth));
        
        // Cleanup
        Object.DestroyImmediate(target.gameObject);
        Object.DestroyImmediate(combatSystem.gameObject);
    }
}