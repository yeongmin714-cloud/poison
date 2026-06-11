using NUnit.Framework;

public class SampleTests
{
    [Test]
    public void SampleTest_Passes()
    {
        int a = 1;
        int b = 2;
        int sum = a + b;
        Assert.AreEqual(3, sum);
    }
}