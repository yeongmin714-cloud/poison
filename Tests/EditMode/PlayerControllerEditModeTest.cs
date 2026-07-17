using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;

public class PlayerControllerEditModeTest
{
    [Test]
    public void PlayerControllerHasRequiredComponents()
    {
        // Arrange
        var player = new GameObject("Player");
        var controller = player.AddComponent<PlayerController>();
        
        // Act & Assert
        Assert.That(controller.PlayerMovement, Is.Not.Null);
        Assert.That(controller.PlayerCamera, Is.Not.Null);
        Assert.That(controller.PlayerInput, Is.Not.Null);
        Assert.That(controller.PlayerHealth, Is.Not.Null);
        
        // Cleanup
        Object.DestroyImmediate(player);
    }
    
    [Test]
    public void PlayerControllerCanSetMovementSpeed()
    {
        // Arrange
        var player = new GameObject("Player");
        var controller = player.AddComponent<PlayerController>();
        var newSpeed = 10f;
        
        // Act
        controller.SetMovementSpeed(newSpeed);
        
        // Assert
        Assert.That(controller.MovementSpeed, Is.EqualTo(newSpeed));
        
        // Cleanup
        Object.DestroyImmediate(player);
    }
}