"""Combat / rest-site exchanges between the contracted figure's spirit (S) and the reaper 무명 (R).
Used by MumyeongCode/Dialogue/SpiritTalk.cs; keys: MUMYEONG-SPIRIT_TALK.<FIG>.<SITUATION>.<n>.S / .R (events table).

Writing rules (user 2026-10-24: "never make up data"):
  - The lines are game dialogue in the figure's voice, not quotations. Where a line leans on a recorded event it is one
    that is documented (sources in docs/mumyeong-spec.md §11), e.g. 관우 killing 안량 at 백마, 잔 다르크 at Orléans /
    Reims / Compiègne, 이순신's war diary and the 학익진 at 한산도, 나폴레옹's artillery start and Austerlitz.
  - 이순신 CHAT.3 echoes "必死則生 必生則死", which he wrote in his war diary on 1597-09-15 (정유년) quoting the
    military classics; the line says so instead of presenting it as his own invention.
  - The reaper says little. He does not remember his name.
Each entry: (S kor, R kor, S eng, R eng); R may be "" (the spirit speaks alone).
"""

SITUATIONS = ["COMBAT_START", "ELITE", "BOSS", "LOW_HP", "VICTORY", "BOSS_VICTORY", "REST", "CHAT"]

TALK = {
    "GUANYU": {
        "COMBAT_START": [
            ("칼을 들어라. 망설임은 칼끝을 무디게 한다.", "망설인 적 없어.", "Raise your blade. Hesitation dulls the edge.", "I wasn't hesitating."),
            ("적의 수를 세지 마라. 네가 벨 하나만 보면 된다.", "", "Do not count them. See only the one you will cut.", ""),
            ("의리 없는 싸움은 없다. 너는 무엇을 위해 싸우느냐.", "아직 몰라. 그래서 싸워.", "No fight is without a reason. What is yours?", "Don't know yet. That's why I fight."),
        ],
        "ELITE": [
            ("강한 자로구나. 예를 갖춰 상대하자.", "예의는 칼로 차리지.", "A strong one. Meet him with respect.", "I'll pay my respects with the blade."),
            ("백마에서 안량의 목을 벨 때도 이런 기세였다.", "그때 얘기는 이기고 나서 해.", "I felt this same weight at Baima, before Yan Liang fell.", "Tell me after we win."),
        ],
        "BOSS": [
            ("이 길의 끝을 지키는 자다. 물러설 곳은 없다.", "물러설 생각도 없어.", "This one guards the end of the road. There is nowhere to fall back.", "Wasn't planning to."),
            ("내 수염이 떨리는 건 두려움 때문이 아니다.", "알아. 나도야.", "My beard trembles, but not from fear.", "I know. Same here."),
        ],
        "LOW_HP": [
            ("쓰러지더라도 등을 보이지 마라.", "…안 보여.", "Fall if you must. Never show your back.", "...I won't."),
            ("피를 흘린다고 의(義)가 줄지는 않는다. 일어나라.", "", "Blood lost is not honour lost. Stand.", ""),
        ],
        "VICTORY": [
            ("칼을 거둬라. 이긴 뒤의 칼은 오만해지기 쉽다.", "알았어.", "Sheathe it. A blade grows arrogant after victory.", "Fine."),
            ("나쁘지 않았다. 다음엔 한 번에 끝내라.", "욕심이 많네.", "Not bad. Next time, finish it in one stroke.", "You want a lot."),
            ("너는 말이 적구나. 마음에 든다.", "", "You speak little. I like that.", ""),
        ],
        "BOSS_VICTORY": [
            ("문을 지키던 자가 쓰러졌다. 다음 문으로 가자.", "앞장서.", "The gatekeeper has fallen. On to the next gate.", "Lead the way."),
        ],
        "REST": [
            ("불가에 앉으니 형님, 익덕과 한 방에서 지새우던 밤이 떠오르는구나.", "난 그런 사람이 없었어.", "A fire like this... I remember nights in one room with my elder brother and Yide.", "I never had anyone like that."),
            ("이 불이 꺼지기 전에 칼날을 살펴 두거라.", "벌써 했어.", "Check your blade before the fire dies.", "Already did."),
            ("떠나는 길이 멀어도, 돌아갈 곳이 있으면 견딜 만하다.", "돌아갈 곳… 기억이 안 나.", "A long road is bearable if there is somewhere to return to.", "Somewhere to return to... I can't remember."),
        ],
        "CHAT": [
            ("왜 부르느냐. 할 말이 있으면 똑바로 해라.", "그냥. 거기 있나 싶어서.", "Why do you call? Speak plainly.", "Just checking you're there."),
            ("너의 얼굴이 보이지 않는다. 그래도 눈빛은 느껴진다.", "얼굴은 나도 못 봤어.", "I cannot see your face. I can still feel your stare.", "I haven't seen it either."),
            ("한때 나는 조조의 은혜를 입었다. 그 빚을 갚고 나서야 형님께 돌아갔다.", "빚은 다 갚고 가는 성격이구나.", "Once I owed Cao Cao a debt. I repaid it before I went back to my brother.", "You pay what you owe before you leave."),
            ("너는 이름이 없다지. 이름보다 행적이 오래 남는다.", "그럼 행적부터 남기지.", "They say you have no name. Deeds outlast names.", "Then I'll start with the deeds."),
        ],
    },
    "JOAN": {
        "COMBAT_START": [
            ("깃발을 높이! 우리가 앞에 서면 모두가 따라온다.", "따라올 사람이 나밖에 없는데.", "Raise the banner! If we stand in front, everyone follows.", "There's only me to follow."),
            ("두려워해도 괜찮아. 그래도 앞으로 가.", "", "It's all right to be afraid. Go forward anyway.", ""),
            ("목소리가 들려. 오늘도 싸우래.", "나한텐 안 들리는데.", "The voices again. They say fight today.", "I can't hear them."),
        ],
        "ELITE": [
            ("오를레앙의 성벽도 처음엔 이렇게 높아 보였어.", "그래서 넘었어?", "The walls of Orléans looked this high at first.", "And you got over them?"),
            ("화살이 날아와도 깃발은 내려놓지 않을 거야.", "", "Arrows or not, I won't lower the banner.", ""),
        ],
        "BOSS": [
            ("이 싸움이 끝나면 길이 열릴 거야. 믿어.", "믿는 건 네가 해. 난 싸울게.", "When this ends the road will open. Believe it.", "You believe. I'll fight."),
            ("무서워? 나도 그래. 그러니까 같이 가자.", "…같이 가.", "Scared? So am I. So let's go together.", "...Together."),
        ],
        "LOW_HP": [
            ("일어나! 나도 화살을 맞고 다시 일어났어.", "…알았어.", "Get up! I took an arrow once and stood again.", "...Got it."),
            ("아직 아니야. 아직 할 일이 남았어.", "", "Not yet. There's still work to do.", ""),
        ],
        "VICTORY": [
            ("해냈어! 봐, 길은 언제나 열려.", "운이 좋았어.", "We did it! See, the way always opens.", "We were lucky."),
            ("다친 데 없어? …연기라서 모르겠다.", "괜찮아.", "Are you hurt? ...I can't tell, you're all smoke.", "I'm fine."),
            ("싸움은 끝났어. 이제 기도하자.", "기도는 네 몫이야.", "The fight is over. Now we pray.", "Praying's your part."),
        ],
        "BOSS_VICTORY": [
            ("랭스에서 왕관이 씌워지던 날처럼 가슴이 뛰어.", "그날도 이렇게 시끄러웠어?", "My heart is pounding like the day of the crowning at Reims.", "Were you this loud then too?"),
        ],
        "REST": [
            ("불 냄새는 좋아하지 않아. 그래도 따뜻하긴 하네.", "…미안. 다른 데로 갈까.", "I don't like the smell of fire. It is warm, though.", "...Sorry. Should we move?"),
            ("고향 마을엔 이맘때 종이 울렸어.", "종소리는 기억나.", "Back home the bells would ring about now.", "I remember bells."),
            ("나는 칼보다 깃발이 좋아. 칼은 아무도 지키지 못하거든.", "깃발도 못 지키잖아.", "I like the banner more than the sword. A sword protects no one.", "Neither does a banner."),
        ],
        "CHAT": [
            ("불렀어? 나 여기 있어.", "알아. 확인만.", "You called? I'm here.", "I know. Just checking."),
            ("콩피에뉴에서 붙잡혔을 때, 끝까지 성으로 돌아가려 했어.", "끝까지 갔네.", "When they caught me at Compiègne, I was still trying to get back to the town.", "You went to the end."),
            ("넌 왜 얼굴을 가리고 다녀?", "가린 게 아니야. 없는 거야.", "Why do you hide your face?", "I'm not hiding it. It's gone."),
            ("이름이 없어도 괜찮아. 나도 처음엔 그냥 마을 아이였어.", "", "No name is fine. I was just a village girl once.", ""),
        ],
    },
    "LEONIDAS": {
        "COMBAT_START": [
            ("방패를 들어라. 방패는 옆 사람을 위한 것이다.", "옆에 너밖에 없어.", "Shield up. A shield is for the man beside you.", "Only you're beside me."),
            ("좁은 길이다. 좁은 길은 적은 수로도 지킬 수 있다.", "", "A narrow pass. A narrow pass can be held by few.", ""),
            ("숨 고르지 마라. 스파르타는 숨이 차도 싸운다.", "난 숨 안 쉬어.", "Don't catch your breath. Sparta fights winded.", "I don't breathe."),
        ],
        "ELITE": [
            ("크군. 큰 자일수록 무릎이 약하다.", "어디 보자.", "Big one. The bigger they are, the weaker the knees.", "Let's see."),
            ("내 방패 뒤로 와라. 아니, 네가 내 방패가 되어라.", "둘 다 해 볼게.", "Get behind my shield. No — be my shield.", "I'll try both."),
        ],
        "BOSS": [
            ("무기를 내려놓으라 하거든, 와서 가져가라고 해라.", "와서 가져가.", "If they tell you to lay down your arms, tell them to come and take them.", "Come and take them."),
            ("여기가 우리의 테르모필레다.", "", "This is our Thermopylae.", ""),
        ],
        "LOW_HP": [
            ("물러서지 마. 쓰러질 거라면 앞으로 쓰러져라.", "…앞으로.", "Don't step back. If you fall, fall forward.", "...Forward."),
            ("방패를 들고 돌아오거나, 방패 위에 실려 돌아오는 거다.", "둘 다 싫어.", "Come back with your shield, or on it.", "I don't like either."),
        ],
        "VICTORY": [
            ("좋다. 오늘 밤은 굶지 않아도 되겠군.", "난 원래 안 먹어.", "Good. We won't go hungry tonight.", "I don't eat anyway."),
            ("방패에 흠집이 늘었다. 그만큼 네가 살아 있다는 뜻이다.", "", "More dents in the shield. That's how much you're still alive.", ""),
            ("말이 길면 패배자다. 다음.", "다음.", "Long speeches are for losers. Next.", "Next."),
        ],
        "BOSS_VICTORY": [
            ("길목을 지켜냈다. 이번엔 끝까지.", "이번엔.", "We held the pass. All the way, this time.", "This time."),
        ],
        "REST": [
            ("싸우기 전에 머리를 빗는 건 스파르타의 예법이다. 너는… 모자를 벗지 않는군.", "벗으면 큰일 나.", "Combing one's hair before battle is the Spartan way. You... never take off that hat.", "Bad things happen if I do."),
            ("나와 함께 남은 삼백이 있었다.", "다들 어떻게 됐어?", "There were three hundred who stayed with me.", "What happened to them?"),
            ("잘 자라. 내일은 짧게 끝내자.", "", "Sleep. Let's make tomorrow short.", ""),
        ],
        "CHAT": [
            ("할 말이 있으면 짧게.", "짧게. 고마워.", "If you have something to say, keep it short.", "Short. Thanks."),
            ("너는 왜 싸우지?", "누군가 그러라고 했어. 누군지는 기억 안 나.", "Why do you fight?", "Someone told me to. I don't remember who."),
            ("스파르타에서 아이는 일곱 살에 집을 떠난다.", "난 몇 살에 떠났는지도 몰라.", "In Sparta a boy leaves home at seven.", "I don't even know when I left."),
            ("등 뒤는 내가 본다. 앞만 봐라.", "", "I'll watch your back. Look ahead.", ""),
        ],
    },
    "GENGHIS": {
        "COMBAT_START": [
            ("말에 올라라. 초원에서는 멈춘 자가 먼저 죽는다.", "말이 없는데.", "Mount up. On the steppe, whoever stops dies first.", "I don't have a horse."),
            ("포위해라. 한쪽 길만 열어 두면 적은 그리로 도망친다.", "", "Surround them. Leave one road open and they will run down it.", ""),
            ("적이 많군. 좋다. 나눌 전리품도 많다.", "나눌 사람은 없어.", "Many of them. Good. More to share.", "No one to share with."),
        ],
        "ELITE": [
            ("강한 적은 쓰러뜨리고, 쓸 만하면 거둔다.", "이건 못 쓸 것 같은데.", "Defeat the strong, and take them in if they're useful.", "Doesn't look useful."),
            ("화살을 아껴라. 한 발이면 될 자리다.", "", "Save the arrows. One shot is enough here.", ""),
        ],
        "BOSS": [
            ("내가 초원을 하나로 묶을 때도 처음엔 다들 웃었다.", "웃게 둬.", "When I set out to unite the steppe, they laughed at first.", "Let them laugh."),
            ("이 자를 넘으면 세상이 넓어진다.", "", "Get past this one and the world grows wider.", ""),
        ],
        "LOW_HP": [
            ("말에서 떨어졌으면 다시 올라라. 그게 전부다.", "…간단하네.", "If you fall from the horse, get back on. That's all.", "...Simple."),
            ("나는 어릴 때 모든 것을 잃었다. 그래도 살아남았다.", "", "I lost everything as a boy. I still survived.", ""),
        ],
        "VICTORY": [
            ("끝났다. 다음 땅으로.", "쉬지도 않아?", "Done. On to the next land.", "No rest?"),
            ("전리품을 챙겨라. 빈손으로 돌아가는 원정은 없다.", "", "Take the spoils. No campaign goes home empty-handed.", ""),
            ("빠르군. 마음에 든다.", "넌 늘 빠른 게 좋지.", "Fast. I like it.", "You always like fast."),
        ],
        "BOSS_VICTORY": [
            ("또 하나의 땅이 열렸다. 지평선은 아직 멀다.", "끝은 있긴 해?", "Another land opens. The horizon is still far.", "Is there an end?"),
        ],
        "REST": [
            ("초원의 밤은 이것보다 추웠다.", "춥다는 느낌은 기억나.", "Steppe nights were colder than this.", "I remember cold."),
            ("내 이름은 원래 테무진이었다. 이름은 바뀌어도 사람은 그대로다.", "난 바뀔 이름도 없어.", "My name was Temüjin once. Names change; the man stays.", "I don't even have one to change."),
            ("말을 쉬게 하는 것도 전쟁이다.", "", "Resting the horses is part of war too.", ""),
        ],
        "CHAT": [
            ("무엇이냐. 지시를 원하느냐?", "아니. 그냥.", "What? You want orders?", "No. Just because."),
            ("믿을 수 있는 자 하나가 군대 하나보다 낫다.", "날 믿어?", "One man you can trust is worth an army.", "Do you trust me?"),
            ("넌 연기로 된 얼굴이군. 적이 읽을 수 없는 얼굴이다. 좋다.", "칭찬이야?", "A face of smoke. An enemy can't read it. Good.", "Is that praise?"),
            ("초원에서는 소식이 말보다 빨라야 한다.", "", "On the steppe, news must ride faster than horses.", ""),
        ],
    },
    "YISUNSIN": {
        "COMBAT_START": [
            ("서두르지 마라. 적이 들어올 자리를 먼저 정하라.", "어디가 좋을까.", "Do not hurry. First decide where the enemy will come in.", "Where would be good?"),
            ("포를 준비하라. 가까이 올수록 우리에게 유리하다.", "", "Ready the cannon. The closer they come, the better for us.", ""),
            ("오늘 일도 일기에 적어 두겠다.", "좋게 써 줘.", "I will write today down in my diary as well.", "Write it kindly."),
        ],
        "ELITE": [
            ("한산 앞바다에서는 학의 날개처럼 펼쳐 감쌌다. 지금도 그렇게 하자.", "날개는 네가 맡아.", "Off Hansan we spread out like a crane's wings and closed in. Let's do it again.", "You take the wings."),
            ("수가 적다고 지는 것이 아니다.", "", "Being few is not the same as losing.", ""),
        ],
        "BOSS": [
            ("물살이 바뀌는 때를 기다려라. 그때가 우리의 때다.", "언제 바뀌는데?", "Wait for the tide to turn. That moment is ours.", "When does it turn?"),
            ("두려움은 퍼진다. 그러니 장수는 먼저 떨지 않는다.", "안 떨어.", "Fear spreads. So a commander does not tremble first.", "I'm not trembling."),
        ],
        "LOW_HP": [
            ("아직 끝나지 않았다. 내가 쓰러졌을 때도 싸움은 계속되었다.", "…계속할게.", "It is not over. Even when I fell, the battle went on.", "...I'll keep going."),
            ("숨을 고르고, 다시 정비하라.", "", "Breathe, and set your lines again.", ""),
        ],
        "VICTORY": [
            ("이겼다. 다친 자부터 살펴라.", "나밖에 없어.", "We won. See to the wounded first.", "There's only me."),
            ("오늘 쓴 포탄과 화살을 세어 두어라. 내일을 위해서다.", "꼼꼼하네.", "Count what we used today. It's for tomorrow.", "You're thorough."),
            ("좋은 싸움이었다. 그러나 방심하지 마라.", "", "A good fight. Do not let your guard down.", ""),
        ],
        "BOSS_VICTORY": [
            ("큰 적을 물리쳤다. 그러나 바다는 아직 넓다.", "그럼 계속 가.", "A great enemy is beaten. But the sea is still wide.", "Then we keep going."),
        ],
        "REST": [
            ("밤마다 등불 아래서 그날을 적었다. 너도 적어 보아라.", "쓸 이름이 없어.", "Every night I wrote the day down by lamplight. You should too.", "I've no name to sign it with."),
            ("어머니 소식을 기다리던 밤이 있었다.", "…기다리는 사람이 있었구나.", "There were nights I waited for word of my mother.", "...You had someone to wait for."),
            ("배를 고치는 날이 싸우는 날보다 많았다.", "", "There were more days mending ships than days fighting.", ""),
        ],
        "CHAT": [
            ("무슨 일이냐.", "아무 일도. 그냥 불렀어.", "What is it?", "Nothing. I just called."),
            ("백성을 지키는 것이 장수의 일이다. 너는 무엇을 지키느냐.", "지금은… 너.", "A commander's work is protecting the people. What do you protect?", "Right now... you."),
            ("병법에 '죽고자 하면 살고, 살고자 하면 죽는다'고 했다. 내가 일기에 옮겨 적었던 말이다.", "어려운 말이야.", "The art of war says: seek death and live, seek life and die. I copied it into my diary.", "That's a hard one."),
            ("네 모자 그림자 아래가 바다 밑처럼 어둡구나.", "", "Under your hat it is dark as the sea floor.", ""),
        ],
    },
    "NAPOLEON": {
        "COMBAT_START": [
            ("포병을 앞으로! 승리는 대포 소리와 함께 온다.", "대포 없는데.", "Artillery forward! Victory arrives with the guns.", "We don't have guns."),
            ("적의 중심을 봐라. 거기를 꺾으면 날개는 알아서 무너진다.", "", "Look at their centre. Break it and the wings collapse on their own.", ""),
            ("빠르게. 전쟁에서 잃은 시간은 다시 오지 않는다.", "알았어, 알았어.", "Quickly. Time lost in war never comes back.", "Okay, okay."),
        ],
        "ELITE": [
            ("훌륭한 적이군. 이런 적을 이겨야 이름이 남지.", "난 이름이 없는데.", "A fine enemy. Beat ones like this and your name is remembered.", "I don't have a name."),
            ("병력을 한곳에 모아라. 흩어진 힘은 힘이 아니다.", "", "Concentrate your force. Scattered strength is no strength.", ""),
        ],
        "BOSS": [
            ("아우스터리츠의 아침처럼 해가 뜨는구나.", "여긴 해가 안 떠.", "The sun is rising, like the morning of Austerlitz.", "The sun doesn't rise here."),
            ("나는 섬에서 돌아온 적도 있다. 이 정도는 아무것도 아니야.", "", "I came back from an island once. This is nothing.", ""),
        ],
        "LOW_HP": [
            ("후퇴는 패배가 아니다. 다시 공격하기 위한 준비다.", "물러설 데가 없어.", "A retreat is not a defeat. It's preparation for the next attack.", "Nowhere to retreat to."),
            ("버텨라! 예비대는 아직 투입하지 않았다.", "예비대가 있어?", "Hold! The reserve hasn't been committed yet.", "We have a reserve?"),
        ],
        "VICTORY": [
            ("완벽한 기동이었다. 물론 내 지휘 덕분이지.", "그래, 그래.", "A perfect manoeuvre. Thanks to my command, naturally.", "Sure, sure."),
            ("전과를 보고하라. …아, 너밖에 없지.", "보고할게. 이겼어.", "Report the results. ...Ah, it's only you.", "Reporting. We won."),
            ("이긴 다음 날이 더 중요하다.", "", "The day after a victory matters more.", ""),
        ],
        "BOSS_VICTORY": [
            ("대승이다! 이 날을 기념하는 문을 세워야겠군.", "세울 데가 없어.", "A great victory! We should raise an arch for this day.", "Nowhere to build it."),
        ],
        "REST": [
            ("너는 아예 잠을 자지 않는군. 지휘관으로서 부럽다.", "잠은 죽기 전에 다 잤어.", "You don't sleep at all. As a commander, I envy that.", "I did all my sleeping before I died."),
            ("포병 장교로 시작했다. 계산이 맞으면 포탄은 거짓말을 안 해.", "", "I started as an artillery officer. Get the numbers right and the shell never lies.", ""),
            ("지도를 펼쳐라. 다음 진격로를 짜자.", "지도엔 물음표뿐이야.", "Unroll the map. Let's plan the next advance.", "The map's all question marks."),
        ],
        "CHAT": [
            ("무슨 용무인가? 짧게 보고하라.", "이상 없음.", "What is it? Report briefly.", "Nothing to report."),
            ("그 모자, 나쁘지 않군. 내 것만은 못하지만.", "넌 모자를 옆으로 쓰잖아.", "That hat isn't bad. Not as good as mine.", "You wear yours sideways."),
            ("작은 섬에서 태어나 대륙을 흔들었다. 너도 못 할 건 없다.", "난 어디서 태어났는지도 몰라.", "Born on a small island, I shook a continent. You can do it too.", "I don't know where I was born."),
            ("보급이 끊기면 용기도 끊긴다. 잊지 마라.", "", "Cut the supply and you cut the courage. Remember that.", ""),
        ],
    },
}
