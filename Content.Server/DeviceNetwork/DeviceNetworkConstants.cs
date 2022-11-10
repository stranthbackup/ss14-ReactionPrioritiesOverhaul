using Content.Server.DeviceNetwork.Components;

namespace Content.Server.DeviceNetwork
{
    /// <summary>
    /// A collection of constants to help with using device networks
    /// </summary>
    public static class DeviceNetworkConstants
    {
        #region Commands

        /// <summary>
        /// The key for command names
        /// E.g. [DeviceNetworkConstants.Command] = "ping"
        /// </summary>
        public const string Command = "command";

        /// <summary>
        /// The command for setting a devices state
        /// E.g. to turn a light on or off
        /// </summary>
        public const string CmdSetState = "set_state";

        /// <summary>
        /// The command for a device that just updated its state
        /// E.g. suit sensors broadcasting owners vitals state
        /// </summary>
        public const string CmdUpdatedState = "updated_state";

        #endregion

        #region SetState

        /// <summary>
        /// Used with the <see cref="CmdSetState"/> command to turn a device on or off
        /// </summary>
        public const string StateEnabled = "state_enabled";

        #endregion

        #region DisplayHelpers

        /// <summary>
        /// Converts the unsigned int to string and inserts a number before the last digit
        /// </summary>
        public static string FrequencyToString(this uint frequency)
        {
            var result = frequency.ToString();
            if (result.Length <= 2)
                return result + ".0";

            return result.Insert(result.Length - 1, ".");
        }

        /// <summary>
        /// Either returns the string representation of the corresponding <see cref="DeviceNetworkComponent.DeviceNetIdDefaults"/>
        /// or converts the id to string
        /// </summary>
        public static string DeviceNetIdToString(this int id)
        {
            var result = Enum.IsDefined(typeof(DeviceNetworkComponent.DeviceNetIdDefaults), id)
                ? ((DeviceNetworkComponent.DeviceNetIdDefaults)id).ToString()
                : id.ToString();

            return result;
        }

        #endregion
    }
}
