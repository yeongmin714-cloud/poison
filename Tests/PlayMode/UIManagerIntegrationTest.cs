using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;

public class UIManagerIntegrationTest
{
    [Test]
    public void UIManagerInitializesCorrectly()
    {
        // Arrange
        var uiManager = new GameObject("UIManager").AddComponent<UIManager>();
        
        // Act
        uiManager.Initialize();
        
        // Assert
        Assert.That(uiManager.IsInitialized, Is.True);
        
        // Cleanup
        Object.DestroyImmediate(uiManager.gameObject);
    }
    
    [Test]
    public void UIManagerCanShowPauseMenu()
    {
        // Arrange
        var uiManager = new GameObject("UIManager").AddComponent<UIManager>();
        uiManager.Initialize();
        
        // Act
        uiManager.ShowPauseMenu();
        
        // Assert
        Assert.That(uiManager.IsPauseMenuVisible, Is.True);
        
        // Cleanup
        Object.DestroyImmediate(uiManager.gameObject);
    }
}