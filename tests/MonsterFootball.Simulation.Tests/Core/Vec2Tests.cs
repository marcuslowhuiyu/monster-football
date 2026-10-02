using MonsterFootball.Simulation.Core;

namespace MonsterFootball.Simulation.Tests.Core;

public class Vec2Tests
{
    [Fact]
    public void MoveTowards_moves_at_most_max_distance()
    {
        var result = new Vec2(0, 0).MoveTowards(new Vec2(10, 0), 3);
        Assert.Equal(new Vec2(3, 0), result);
    }

    [Fact]
    public void MoveTowards_does_not_overshoot()
    {
        var result = new Vec2(0, 0).MoveTowards(new Vec2(1, 1), 5);
        Assert.Equal(new Vec2(1, 1), result);
    }

    [Fact]
    public void Normalized_zero_vector_is_zero()
    {
        Assert.Equal(Vec2.Zero, Vec2.Zero.Normalized());
    }

    [Fact]
    public void DistanceTo_is_euclidean()
    {
        Assert.Equal(5, new Vec2(0, 0).DistanceTo(new Vec2(3, 4)), precision: 10);
    }
}
