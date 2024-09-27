using System;
using System.Collections.Generic;
using IO.Astrodynamics.TimeSystem;

namespace IO.Astrodynamics;

public class GeometryFinder
{
    public GeometryFinder()
    {
    }

    public bool EvaluateCondition<T>(T actualValue, T targetValue, RelationnalOperator operatorType)
    {
        if (actualValue is double actualDouble && targetValue is double targetDouble)
        {
            switch (operatorType)
            {
                case RelationnalOperator.Greater:
                    return actualDouble > targetDouble;
                case RelationnalOperator.GreaterOrEqual:
                    return actualDouble >= targetDouble;
                case RelationnalOperator.Lower:
                    return actualDouble < targetDouble;
                case RelationnalOperator.LowerOrEqual:
                    return actualDouble <= targetDouble;
                case RelationnalOperator.Equal:
                    return System.Math.Abs(actualDouble - targetDouble) < 1e-9;
                case RelationnalOperator.NotEqual:
                    return System.Math.Abs(actualDouble - targetDouble) >= 1e-9;
                default:
                    throw new ArgumentException("Invalid operator");
            }
        }
        else if (actualValue is bool actualBool && targetValue is bool targetBool)
        {
            switch (operatorType)
            {
                case RelationnalOperator.Equal:
                    return actualBool == targetBool;
                case RelationnalOperator.NotEqual:
                    return actualBool != targetBool;
                default:
                    throw new ArgumentException("Invalid operator");
            }
        }
        else
        {
            throw new ArgumentException("Invalid type");
        }
    }

    public IEnumerable<Window> FindWindowsWithCondition<T>(Window searchWindow,
        Func<Time, T> calculateValue,
        RelationnalOperator relationalOperator, T targetValue,
        TimeSpan stepSize)
    {
        List<Window> validWindows = new List<Window>();
        var precision = TimeSpan.FromSeconds(1);
        Time current = searchWindow.StartDate;
        Time end = searchWindow.EndDate;

        bool isInValidWindow = false;
        Time? validWindowStart = null;

        // Parcourir la plage de temps par stepSize
        while (current <= end)
        {
            // Calculer la valeur actuelle à la date 'current'
            T currentValue = calculateValue(current);

            // Vérifier la condition à la date actuelle
            if (EvaluateCondition<T>(currentValue, targetValue, relationalOperator))
            {
                // Si la condition est satisfaite et qu'on n'est pas déjà dans une fenêtre valide
                if (!isInValidWindow)
                {
                    // Chercher précisément le début de la fenêtre avec une recherche binaire
                    validWindowStart = FindTransition(current - stepSize, current, calculateValue, targetValue, relationalOperator, precision);
                    isInValidWindow = true;
                }
            }
            else
            {
                // Si la condition n'est plus satisfaite et qu'on était dans une fenêtre valide
                if (isInValidWindow)
                {
                    // Chercher précisément la fin de la fenêtre avec une recherche binaire
                    Time validWindowEnd = FindTransition(validWindowStart.Value, current, calculateValue, targetValue, ReverseOperator(relationalOperator), precision);

                    // Ajouter la fenêtre précise à la liste
                    validWindows.Add(new Window(validWindowStart.Value, validWindowEnd));

                    // Réinitialiser le marqueur
                    isInValidWindow = false;
                    validWindowStart = null;
                }
            }

            // Avancer à l'intervalle suivant
            current = current.Add(stepSize);
        }

        // Gérer le cas où la condition est satisfaite jusqu'à la fin de la plage de recherche
        if (isInValidWindow)
        {
            Time validWindowEnd = FindTransition(validWindowStart.Value, end, calculateValue, targetValue, ReverseOperator(relationalOperator), precision);
            validWindows.Add(new Window(validWindowStart.Value, validWindowEnd));
        }

        return validWindows;
    }

    private Time FindTransition<T>(Time start, Time end, Func<Time, T> calculateValue,
        T targetValue, RelationnalOperator relationalOperator, TimeSpan precision)
    {
        while ((end - start) > precision)
        {
            // Calculer le milieu de l'intervalle
            Time mid = start + (end - start) / 2;

            // Calculer la valeur au milieu
            T midValue = calculateValue(mid);

            // Vérifier la condition à la date 'mid'
            if (EvaluateCondition<T>(midValue, targetValue, relationalOperator))
            {
                // Si la condition est satisfaite à 'mid', on continue à chercher vers le début pour trouver la transition exacte
                end = mid;
            }
            else
            {
                // Sinon, on cherche vers la fin
                start = mid;
            }
        }

        // Retourner la date où la transition a été détectée avec la précision requise
        return start;
    }

    private RelationnalOperator ReverseOperator(RelationnalOperator op)
    {
        return op switch
        {
            RelationnalOperator.Greater => RelationnalOperator.LowerOrEqual,
            RelationnalOperator.Lower => RelationnalOperator.GreaterOrEqual,
            RelationnalOperator.Equal => RelationnalOperator.NotEqual,
            RelationnalOperator.LowerOrEqual => RelationnalOperator.Greater,
            RelationnalOperator.GreaterOrEqual => RelationnalOperator.Lower,
            RelationnalOperator.NotEqual => RelationnalOperator.Equal,
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}