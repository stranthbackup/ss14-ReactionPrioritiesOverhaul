using Content.Shared.Damage;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Weapons.Ranged.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedGunSystem))]
public sealed partial class GunComponent : Component
{
    #region Sound

    [DataField]
    public SoundSpecifier? SoundGunshot = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/smg.ogg");

    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public SoundSpecifier? SoundGunshotModified;

    [DataField]
    public SoundSpecifier? SoundEmpty = new SoundPathSpecifier("/Audio/Weapons/Guns/Empty/empty.ogg");

    /// <summary>
    /// Sound played when toggling the <see cref="SelectedMode"/> for this gun.
    /// </summary>
    [DataField]
    public SoundSpecifier? SoundMode = new SoundPathSpecifier("/Audio/Weapons/Guns/Misc/selector.ogg");

    #endregion

    #region Recoil

    // These values are very small for now until we get a debug overlay and fine tune it

    /// <summary>
    /// The base scalar value applied to the vector governing camera recoil.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float CameraRecoilScalar = 1f;

    /// <summary>
    /// A scalar value applied to the vector governing camera recoil.
    /// If 0, there will be no camera recoil.
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float CameraRecoilScalarModified = 1f;

    /// <summary>
    /// Last time the gun fired.
    /// Used for recoil purposes.
    /// </summary>
    [DataField]
    public TimeSpan LastFire = TimeSpan.Zero;

    /// <summary>
    /// What the current spread is for shooting. This gets changed every time the gun fires.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public Angle CurrentAngle;

    /// <summary>
    /// The base value for how much the spread increases every time the gun fires.
    /// </summary>
    [DataField]
    public Angle AngleIncrease = Angle.FromDegrees(0.5);

    /// <summary>
    /// How much the spread increases every time the gun fires.
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public Angle AngleIncreaseModified;

    /// <summary>
    /// The base value for how much the <see cref="CurrentAngle"/> decreases per second.
    /// </summary>
    [DataField]
    public Angle AngleDecay = Angle.FromDegrees(4);

    /// <summary>
    /// How much the <see cref="CurrentAngle"/> decreases per second.
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public Angle AngleDecayModified;

    /// <summary>
    /// The base value for the maximum angle allowed for <see cref="CurrentAngle"/>
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public Angle MaxAngle = Angle.FromDegrees(2);

    /// <summary>
    /// The maximum angle allowed for <see cref="CurrentAngle"/>
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public Angle MaxAngleModified;

    /// <summary>
    /// The base value for the minimum angle allowed for <see cref="CurrentAngle"/>
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public Angle MinAngle = Angle.FromDegrees(1);

    /// <summary>
    ///  The minimum angle allowed for <see cref="CurrentAngle"/>.
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public Angle MinAngleModified;

    #endregion

    /// <summary>
    /// Whether this gun is shot via the use key or the alt-use key.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool UseKey = true;

    /// <summary>
    /// Where the gun is being requested to shoot.
    /// </summary>
    [ViewVariables]
    public EntityCoordinates? ShootCoordinates = null;

    /// <summary>
    ///     The base value for how many shots to fire per burst.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int ShotsPerBurst = 3;

    /// <summary>
    ///     How many shots to fire per burst.
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public int ShotsPerBurstModified = 3;

    /// <summary>
    /// Used for tracking semi-auto / burst
    /// </summary>
    [ViewVariables]
    [AutoNetworkedField]
    public int ShotCounter = 0;

    /// <summary>
    /// The base value for how many times it shoots per second.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public float FireRate = 8f;

    /// <summary>
    /// How many times it shoots per second.
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float FireRateModified;

    /// <summary>
    /// Starts fire cooldown when equipped if true.
    /// </summary>
    [DataField]
    public bool ResetOnHandSelected = true;

    /// <summary>
    /// Type of ammo the gun can work with
    /// </summary>
    [DataField]
    public List<ProtoId<TagPrototype>>? CompatibleAmmo;

    /// <summary>
    /// Damage the gun deals when used with wrong ammo
    /// </summary>
    [DataField]
    public DamageSpecifier? DamageOnWrongAmmo = null;

    /// <summary>
    /// The base value for how fast the projectile moves.
    /// </summary>
    [DataField]
    public float ProjectileSpeed = 25f;

    /// <summary>
    /// How fast the projectile moves.
    /// </summary>
    [AutoNetworkedField, ViewVariables(VVAccess.ReadWrite)]
    public float ProjectileSpeedModified;

    /// <summary>
    /// When the gun is next available to be shot.
    /// Can be set multiple times in a single tick due to guns firing faster than a single tick time.
    /// </summary>
    [DataField(customTypeSerializer:typeof(TimeOffsetSerializer))]
    [AutoNetworkedField]
    public TimeSpan NextFire = TimeSpan.Zero;

    /// <summary>
    /// What firemodes can be selected.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public SelectiveFire AvailableModes = SelectiveFire.SemiAuto;

    /// <summary>
    /// What firemode is currently selected.
    /// </summary>
    [DataField]
    [AutoNetworkedField]
    public SelectiveFire SelectedMode = SelectiveFire.SemiAuto;

    /// <summary>
    /// Whether or not information about
    /// the gun will be shown on examine.
    /// </summary>
    [DataField]
    public bool ShowExamineText = true;

    /// <summary>
    /// Whether or not someone with the
    /// clumsy trait can shoot this
    /// </summary>
    [DataField]
    public bool ClumsyProof = false;
}

[Flags]
public enum SelectiveFire : byte
{
    Invalid = 0,
    // Combat mode already functions as the equivalent of Safety
    SemiAuto = 1 << 0,
    Burst = 1 << 1,
    FullAuto = 1 << 2, // Not in the building!
}
