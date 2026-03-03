namespace PokerServer.Services;

public static class PokerHandEvaluator
{
    public enum HandRank
    {
        HighCard = 0,
        OnePair = 1,
        TwoPair = 2,
        ThreeOfAKind = 3,
        Straight = 4,
        Flush = 5,
        FullHouse = 6,
        FourOfAKind = 7,
        StraightFlush = 8,
        RoyalFlush = 9
    }

    public class HandResult
    {
        public HandRank Rank { get; set; }
        public int[] TieBreakers { get; set; } = [];
        public string Description { get; set; } = string.Empty;

        public int CompareTo(HandResult other)
        {
            if (Rank != other.Rank)
                return Rank.CompareTo(other.Rank);

            for (int i = 0; i < Math.Min(TieBreakers.Length, other.TieBreakers.Length); i++)
            {
                if (TieBreakers[i] != other.TieBreakers[i])
                    return TieBreakers[i].CompareTo(other.TieBreakers[i]);
            }
            return 0;
        }
    }

    public static HandResult EvaluateHand(List<(string Suit, string Value)> cards)
    {
        if (cards.Count < 5)
            return new HandResult { Rank = HandRank.HighCard, Description = "Not enough cards" };

        // Get all 5-card combinations from available cards (up to 7)
        var bestHand = new HandResult { Rank = HandRank.HighCard };

        foreach (var combination in GetCombinations(cards, 5))
        {
            var result = EvaluateFiveCards(combination);
            if (result.CompareTo(bestHand) > 0)
                bestHand = result;
        }

        return bestHand;
    }

    private static HandResult EvaluateFiveCards(List<(string Suit, string Value)> cards)
    {
        var values = cards.Select(c => GetValueRank(c.Value)).OrderByDescending(v => v).ToArray();
        var suits = cards.Select(c => c.Suit).ToArray();

        bool isFlush = suits.Distinct().Count() == 1;
        bool isStraight = IsStraight(values, out int highCard);
        
        // Check for Ace-low straight (A-2-3-4-5)
        bool isLowStraight = values.Contains(14) && values.Contains(2) && values.Contains(3) && 
                             values.Contains(4) && values.Contains(5);
        if (isLowStraight)
        {
            isStraight = true;
            highCard = 5;
        }

        var groups = values.GroupBy(v => v).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).ToList();
        var counts = groups.Select(g => g.Count()).ToArray();

        // Royal Flush
        if (isFlush && isStraight && highCard == 14)
            return new HandResult { Rank = HandRank.RoyalFlush, TieBreakers = [14], Description = "Royal Flush" };

        // Straight Flush
        if (isFlush && isStraight)
            return new HandResult { Rank = HandRank.StraightFlush, TieBreakers = [highCard], Description = $"Straight Flush, {GetValueName(highCard)} high" };

        // Four of a Kind
        if (counts.Length >= 1 && counts[0] == 4)
            return new HandResult { Rank = HandRank.FourOfAKind, TieBreakers = [groups[0].Key, groups[1].Key], Description = $"Four of a Kind, {GetValueName(groups[0].Key)}s" };

        // Full House
        if (counts.Length >= 2 && counts[0] == 3 && counts[1] == 2)
            return new HandResult { Rank = HandRank.FullHouse, TieBreakers = [groups[0].Key, groups[1].Key], Description = $"Full House, {GetValueName(groups[0].Key)}s over {GetValueName(groups[1].Key)}s" };

        // Flush
        if (isFlush)
            return new HandResult { Rank = HandRank.Flush, TieBreakers = values, Description = $"Flush, {GetValueName(values[0])} high" };

        // Straight
        if (isStraight)
            return new HandResult { Rank = HandRank.Straight, TieBreakers = [highCard], Description = $"Straight, {GetValueName(highCard)} high" };

        // Three of a Kind
        if (counts.Length >= 1 && counts[0] == 3)
            return new HandResult { Rank = HandRank.ThreeOfAKind, TieBreakers = [groups[0].Key, ..values.Where(v => v != groups[0].Key).Take(2)], Description = $"Three of a Kind, {GetValueName(groups[0].Key)}s" };

        // Two Pair
        if (counts.Length >= 2 && counts[0] == 2 && counts[1] == 2)
            return new HandResult { Rank = HandRank.TwoPair, TieBreakers = [groups[0].Key, groups[1].Key, groups[2].Key], Description = $"Two Pair, {GetValueName(groups[0].Key)}s and {GetValueName(groups[1].Key)}s" };

        // One Pair
        if (counts.Length >= 1 && counts[0] == 2)
            return new HandResult { Rank = HandRank.OnePair, TieBreakers = [groups[0].Key, ..values.Where(v => v != groups[0].Key).Take(3)], Description = $"Pair of {GetValueName(groups[0].Key)}s" };

        // High Card
        return new HandResult { Rank = HandRank.HighCard, TieBreakers = values, Description = $"High Card, {GetValueName(values[0])}" };
    }

    private static bool IsStraight(int[] values, out int highCard)
    {
        highCard = values[0];
        var sorted = values.Distinct().OrderByDescending(v => v).ToArray();
        if (sorted.Length < 5) return false;

        for (int i = 0; i < sorted.Length - 4; i++)
        {
            bool isStraight = true;
            for (int j = 0; j < 4; j++)
            {
                if (sorted[i + j] - sorted[i + j + 1] != 1)
                {
                    isStraight = false;
                    break;
                }
            }
            if (isStraight)
            {
                highCard = sorted[i];
                return true;
            }
        }
        return false;
    }

    private static int GetValueRank(string value)
    {
        return value switch
        {
            "2" => 2, "3" => 3, "4" => 4, "5" => 5, "6" => 6,
            "7" => 7, "8" => 8, "9" => 9, "10" => 10,
            "Jack" => 11, "Queen" => 12, "King" => 13, "Ace" => 14,
            _ => 0
        };
    }

    private static string GetValueName(int rank)
    {
        return rank switch
        {
            11 => "Jack",
            12 => "Queen",
            13 => "King",
            14 => "Ace",
            _ => rank.ToString()
        };
    }

    private static IEnumerable<List<T>> GetCombinations<T>(List<T> list, int length)
    {
        if (length == 0)
        {
            yield return new List<T>();
            yield break;
        }

        for (int i = 0; i <= list.Count - length; i++)
        {
            foreach (var combination in GetCombinations(list.Skip(i + 1).ToList(), length - 1))
            {
                combination.Insert(0, list[i]);
                yield return combination;
            }
        }
    }
}
