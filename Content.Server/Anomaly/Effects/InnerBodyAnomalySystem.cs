using Content.Server.Body.Systems;
using Content.Server.Chat.Managers;
using Content.Server.Jittering;
using Content.Server.Mind;
using Content.Server.Stunnable;
using Content.Shared.Actions;
using Content.Shared.Anomaly.Components;
using Content.Shared.Anomaly.Effects;
using Content.Shared.Body.Components;
using Content.Shared.Chat;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Anomaly.Effects;

public sealed class InnerBodyAnomalySystem : SharedInnerBodyAnomalySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly StunSystem _stun = default!;
    [Dependency] private readonly JitteringSystem _jitter = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InnerBodyAnomalyComponent, ComponentStartup>(OnCompStartup);

        SubscribeLocalEvent<InnerBodyAnomalyComponent, AnomalyShutdownEvent>(OnShutdown);
        SubscribeLocalEvent<InnerBodyAnomalyComponent, AnomalySupercriticalEvent>(OnSupercritical);
    }

    private void OnCompStartup(Entity<InnerBodyAnomalyComponent> ent, ref ComponentStartup args)
    {
        Log.Info($"{ToPrettyString(ent)} become anomaly host!");

        EntityManager.AddComponents(ent, ent.Comp.Components);

        _stun.TryParalyze(ent, TimeSpan.FromSeconds(4f), true);
        _jitter.DoJitter(ent, TimeSpan.FromSeconds(4f), true);

        if (!_mind.TryGetMind(ent, out _, out var mindComponent))
            return;
        if (mindComponent.Session == null)
            return;

        if (ent.Comp.StartSound is not null)
            _audio.PlayPvs(ent.Comp.StartSound, ent);

        if (ent.Comp.StartMessage is not null)
        {
            var message = Loc.GetString(ent.Comp.StartMessage);
            var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
            _chat.ChatMessageToOne(ChatChannel.Server,
                message,
                wrappedMessage,
                default,
                false,
                mindComponent.Session.Channel,
                Color.Red);
        }

        if (ent.Comp.ActionProto is not null)
        {
            var action = _actionContainer.AddAction(ent, ent.Comp.ActionProto);

            if (action is not null)
            {
                ent.Comp.Action = action.Value;
                _actions.GrantActions(ent, new List<EntityUid>{action.Value}, ent);
            }
        }
        Dirty(ent);
    }

    private void OnSupercritical(Entity<InnerBodyAnomalyComponent> ent, ref AnomalySupercriticalEvent args)
    {
        if (!TryComp<BodyComponent>(ent, out var body))
            return;

        _body.GibBody(ent, body: body);
    }

    private void OnShutdown(Entity<InnerBodyAnomalyComponent> ent, ref AnomalyShutdownEvent args)
    {
        _stun.TryParalyze(ent, TimeSpan.FromSeconds(4f), true);

        QueueDel(ent.Comp.Action);
        //EntityManager.RemoveComponents(ent, ent.Comp.Components);
        RemComp<InnerBodyAnomalyComponent>(ent);
    }
}
