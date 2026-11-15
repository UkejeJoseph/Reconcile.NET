namespace Reconcile.Net;

/// <summary>Controls which side of a date criterion is allowed to lag the other.</summary>
public enum DateWindowDirection
{
    /// <summary>Either date may be later than the other, as long as they are within the window.</summary>
    Either = 0,

    /// <summary>Only the left date may be later than the right date, by at most the window. Equal dates always match.</summary>
    LeftMayBeLater = 1,

    /// <summary>Only the right date may be later than the left date, by at most the window. Equal dates always match.</summary>
    RightMayBeLater = 2,
}
