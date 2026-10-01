namespace Nuntiator.Tests;

public class UnitTests
{
    [Fact]
    public void Unit_EqualityAndComparison_BehaveCorrectly()
    {
        var u1 = Unit.Value;
        var u2 = new Unit();

        Assert.Equal(u1, u2);
        Assert.True(u1 == u2);
        Assert.False(u1 != u2);
        Assert.True(u1.Equals((object)u2));
        Assert.Equal(0, u1.GetHashCode());
        Assert.Equal("()", u1.ToString());
        Assert.Equal(0, u1.CompareTo(u2));

        IComparable comparable = u1;
        Assert.Equal(0, comparable.CompareTo(u2));
        Assert.Equal(1, comparable.CompareTo(null));
        Assert.Throws<ArgumentException>(() => comparable.CompareTo("not-a-unit"));
    }

    [Fact]
    public async Task Unit_TaskAndValueTask_AreCompleted()
    {
        var task = Unit.Task;
        Assert.True(task.IsCompletedSuccessfully);
        Assert.Equal(Unit.Value, await task);

        var valueTask = Unit.ValueTask;
        Assert.True(valueTask.IsCompletedSuccessfully);
        Assert.Equal(Unit.Value, await valueTask);
    }
}
