// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;

namespace Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

public abstract class AbstractPathBasedCommandResolver : ICommandResolver
{
    protected IEnvironmentProvider _environment;
    protected IPlatformCommandSpecFactory _commandSpecFactory;

    public AbstractPathBasedCommandResolver(IEnvironmentProvider environment,
        IPlatformCommandSpecFactory commandSpecFactory)
    {
        if (environment == null)
        {
            throw new ArgumentNullException(nameof(environment));
        }

        if (commandSpecFactory == null)
        {
            throw new ArgumentNullException(nameof(commandSpecFactory));
        }

        _environment = environment;
        _commandSpecFactory = commandSpecFactory;
    }

    public CommandSpec Resolve(CommandResolverArguments commandResolverArguments)
    {
        if (commandResolverArguments.CommandName == null)
        {
            return null;
        }

        var commandPath = ResolveCommandPath(commandResolverArguments);

        if (commandPath == null)
        {
            return null;
        }

        return _commandSpecFactory.CreateCommandSpec(
                commandResolverArguments.CommandName,
                commandResolverArguments.CommandArguments.OrEmptyIfNull(),
                commandPath,
                _environment);
    }

    internal abstract string ResolveCommandPath(CommandResolverArguments commandResolverArguments);
}
