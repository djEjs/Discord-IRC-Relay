/*  Discord IRC Relay - A Discord & IRC bot that relays messages */

using IRCRelay.Commands.Handlers;

namespace IRCRelay.Commands
{
	/// <summary>
	/// Builds the shared command dispatcher. Commands are registered in the same
	/// order they appeared in the original Discord if-chain, which preserves the
	/// sequential semantics — most importantly the "~상영회 추가" -> "~추가" chain,
	/// which requires <see cref="ScreeningInfoCommand"/> to run before
	/// <see cref="AppendCommand"/>.
	/// </summary>
	public static class CommandRegistry
	{
		public static CommandDispatcher Build()
		{
			return new CommandDispatcher()
				.Register(new GifCommand())            // ~gif
				.Register(new ApiCopyCommand())        // ~아피
				.Register(new LogCommand())            // ~로그   (both)
				.Register(new DcconCommand())          // ~콘
				.Register(new EmojiPurgeCommand())     // ~이모지숙청
				.Register(new EmojiStatsCommand())     // ~이모지
				.Register(new EmojiResetCommand())     // ~이모지초기화
				.Register(new SaveCommand())           // ~저장   (both)
				.Register(new TrainCommand())          // ~조련 / ~학습
				.Register(new TrainListCommand())      // ~조련목록 / ~학습목록
				.Register(new AiChatCommand())         // ~봇 / ~심심빙봇 (both)
				.Register(new TellCommand())           // ~알려   (both)
				.Register(new FindCommand())           // ~찾아
				.Register(new IrcUserListCommand())    // ~아얄
				.Register(new DiscordUserListCommand())// ~디코   (IRC only)
				.Register(new NickMarkerCommand())     // !닉     (IRC only)
				.Register(new ScreeningExtendCommand())// ~상영회연장
				.Register(new ScreeningInfoCommand())  // ~상영회 / ~심심빙애니 / ~심심빙상영회
				.Register(new AppendCommand())         // ~추가   (must follow ScreeningInfoCommand)
				.Register(new LiveAddCommand())        // ~방송
				.Register(new LiveRemoveCommand())     // ~방송삭제
				.Register(new ScreeningStartCommand()) // ~상영회시작
				.Register(new AlarmToggleCommand())    // ~정각알람
				.Register(new ScreeningJoinCommand())  // ~상영회참가
				.Register(new ScreeningLeaveCommand()) // ~상영회탈퇴 / ~상영회불참
				.Register(new ScreeningEndCommand())   // ~상영회종료
				.Register(new ScreeningExcludeCommand())// ~상영회예외
				.Register(new ChooseCommand())         // ~골라   (both)
				.Register(new EmbedAddCommand())       // ~임베딩추가
				.Register(new EmbedRemoveCommand())    // ~임베딩삭제
				.Register(new EmbedListCommand());     // ~임베딩목록
		}
	}
}
