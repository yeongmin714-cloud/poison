using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;

public class PlayerMovementTest
{
    [Test]
    public void PlayerMovesForward()
    {
        // Arrange
        var player = new GameObject("Player");
        var controller = player.AddComponent<PlayerController>();
        var startPos = player.transform.position;
        
        // Act
        controller.MoveForward();
        
        // Assert
        Assert.That(player.transform.position.z, Is.GreaterThan(startPos.z));
        
        // Cleanup
        Object.DestroyImmediate(player);
    }
    
    [Test]
    public void PlayerMovesBackward()
    {
        // Arrange
        var player = new GameObject("Player");
        var controller = player.AddComponent<PlayerController>();
        var startPos = player.transform.position;
        
        // Act
        controller.MoveBackward();
        
        // Assert
        Assert.That(player.transform.position.z, Is.LessThan(startPos.z));
        
        // Cleanup
        Object.DestroyImmediate(player);
    }
    
    [Test]
    public void PlayerStrafesLeft()
    {
        // Arrange
        var player = new GameObject("Player");
        var controller = player.AddComponent<PlayerController>();
        var startPos = player.transform.position;
        
        // Act
        controller.StrafeLeft();
        
        // Assert
        Assert.That(player.transform.position.x, Is.LessThan(startPos.x));
        
        // Cleanup
        Object.DestroyImmediate(player);
    }
    
    [Test]
    public void PlayerStrafesRight()
    {
        // Arrange
        var player = new GameObject("Player");
        var controller = player.AddComponent<PlayerController>();
        var startPos = player.transform.position;
        
        // Act
        controller.StrafeRight();
        
        // Assert
        Assert.That(player.transform.position.x, Is.GreaterThan(startPos.x));
        
        // Cleanup
        Object.DestroyImmediate(player);
    }
}