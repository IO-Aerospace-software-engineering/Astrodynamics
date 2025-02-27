// Copyright 2023. Sylvain Guillet (sylvain.guillet@tutamail.com)

using IO.Astrodynamics.Body;

namespace IO.Astrodynamics.SolarSystemObjects;

public static class PlanetsAndMoons
{
    public static NaifObject MERCURY = new(199, "MERCURY", "IAU_MERCURY");
    public static NaifObject VENUS = new(299, "VENUS", "IAU_VENUS");
    public static NaifObject EARTH = new(399, "EARTH", "ITRF93");
    public static NaifObject MOON = new(301, "MOON", "IAU_MOON");
    public static NaifObject MARS = new(499, "MARS", "IAU_MARS");
    public static NaifObject PHOBOS = new(401, "PHOBOS", "IAU_PHOBOS");
    public static NaifObject DEIMOS = new(402, "DEIMOS", "IAU_DEIMOS");
    public static NaifObject JUPITER = new(599, "JUPITER", "IAU_JUPITER");
    public static NaifObject IO = new(501, "IO", "IAU_IO");
    public static NaifObject EUROPA = new(502, "EUROPA", "IAU_EUROPA");
    public static NaifObject GANYMEDE = new(503, "GANYMEDE", "IAU_GANYMEDE");
    public static NaifObject CALLISTO = new(504, "CALLISTO", "IAU_CALLISTO");
    public static NaifObject AMALTHEA = new(505, "AMALTHEA", "IAU_AMALTHEA");
    public static NaifObject HIMALIA = new(506, "HIMALIA", "IAU_HIMALIA");
    public static NaifObject ELARA = new(507, "ELARA", "IAU_ELARA");
    public static NaifObject PASIPHAE = new(508, "PASIPHAE", "IAU_PASIPHAE");
    public static NaifObject SINOPE = new(509, "SINOPE", "IAU_SINOPE");
    public static NaifObject LYSITHEA = new(510, "LYSITHEA", "IAU_LYSITHEA");
    public static NaifObject CARME = new(511, "CARME", "IAU_CARME");
    public static NaifObject ANANKE = new(512, "ANANKE", "IAU_ANANKE");
    public static NaifObject LEDA = new(513, "LEDA", "IAU_LEDA");
    public static NaifObject THEBE = new(514, "THEBE", "IAU_THEBE");
    public static NaifObject ADRASTEA = new(515, "ADRASTEA", "IAU_ADRASTEA");
    public static NaifObject METIS = new(516, "METIS", "IAU_METIS");
    public static NaifObject CALLIRRHOE = new(517, "CALLIRRHOE", "IAU_CALLIRRHOE");
    public static NaifObject THEMISTO = new(518, "THEMISTO", "IAU_THEMISTO");
    public static NaifObject MEGACLITE = new(519, "MEGACLITE", "IAU_MEGACLITE");
    public static NaifObject TAYGETE = new(520, "TAYGETE", "IAU_TAYGETE");
    public static NaifObject CHALDENE = new(521, "CHALDENE", "IAU_CHALDENE");
    public static NaifObject HARPALYKE = new(522, "HARPALYKE", "IAU_HARPALYKE");
    public static NaifObject KALYKE = new(523, "KALYKE", "IAU_KALYKE");
    public static NaifObject IOCASTE = new(524, "IOCASTE", "IAU_IOCASTE");
    public static NaifObject ERINOME = new(525, "ERINOME", "IAU_ERINOME");
    public static NaifObject ISONOE = new(526, "ISONOE", "IAU_ISONOE");
    public static NaifObject PRAXIDIKE = new(527, "PRAXIDIKE", "IAU_PRAXIDIKE");
    public static NaifObject AUTONOE = new(528, "AUTONOE", "IAU_AUTONOE");
    public static NaifObject THYONE = new(529, "THYONE", "IAU_THYONE");
    public static NaifObject HERMIPPE = new(530, "HERMIPPE", "IAU_HERMIPPE");
    public static NaifObject AITNE = new(531, "AITNE", "IAU_AITNE");
    public static NaifObject EURYDOME = new(532, "EURYDOME", "IAU_EURYDOME");
    public static NaifObject EUANTHE = new(533, "EUANTHE", "IAU_EUANTHE");
    public static NaifObject EUPORIE = new(534, "EUPORIE", "IAU_EUPORIE");
    public static NaifObject ORTHOSIE = new(535, "ORTHOSIE", "IAU_ORTHOSIE");
    public static NaifObject SPONDE = new(536, "SPONDE", "IAU_SPONDE");
    public static NaifObject KALE = new(537, "KALE", "IAU_KALE");
    public static NaifObject PASITHEE = new(538, "PASITHEE", "IAU_PASITHEE");
    public static NaifObject HEGEMONE = new(539, "HEGEMONE", "IAU_HEGEMONE");
    public static NaifObject MNEME = new(540, "MNEME", "IAU_MNEME");
    public static NaifObject AOEDE = new(541, "AOEDE", "IAU_AOEDE");
    public static NaifObject THELXINOE = new(542, "THELXINOE", "IAU_THELXINOE");
    public static NaifObject ARCHE = new(543, "ARCHE", "IAU_ARCHE");
    public static NaifObject KALLICHORE = new(544, "KALLICHORE", "IAU_KALLICHORE");
    public static NaifObject HELIKE = new(545, "HELIKE", "IAU_HELIKE");
    public static NaifObject CARPO = new(546, "CARPO", "IAU_CARPO");
    public static NaifObject EUKELADE = new(547, "EUKELADE", "IAU_EUKELADE");
    public static NaifObject CYLLENE = new(548, "CYLLENE", "IAU_CYLLENE");
    public static NaifObject KORE = new(549, "KORE", "IAU_KORE");
    public static NaifObject HERSE = new(550, "HERSE", "IAU_HERSE");
    public static NaifObject DIA = new(553, "DIA", "IAU_DIA");
    public static NaifObject SATURN = new(699, "SATURN", "IAU_SATURN");
    public static NaifObject MIMAS = new(601, "MIMAS", "IAU_MIMAS");
    public static NaifObject ENCELADUS = new(602, "ENCELADUS", "IAU_ENCELADUS");
    public static NaifObject TETHYS = new(603, "TETHYS", "IAU_TETHYS");
    public static NaifObject DIONE = new(604, "DIONE", "IAU_DIONE");
    public static NaifObject RHEA = new(605, "RHEA", "IAU_RHEA");
    public static NaifObject TITAN = new(606, "TITAN", "IAU_TITAN");
    public static NaifObject HYPERION = new(607, "HYPERION", "IAU_HYPERION");
    public static NaifObject IAPETUS = new(608, "IAPETUS", "IAU_IAPETUS");
    public static NaifObject PHOEBE = new(609, "PHOEBE", "IAU_PHOEBE");
    public static NaifObject JANUS = new(610, "JANUS", "IAU_JANUS");
    public static NaifObject EPIMETHEUS = new(611, "EPIMETHEUS", "IAU_EPIMETHEUS");
    public static NaifObject HELENE = new(612, "HELENE", "IAU_HELENE");
    public static NaifObject TELESTO = new(613, "TELESTO", "IAU_TELESTO");
    public static NaifObject CALYPSO = new(614, "CALYPSO", "IAU_CALYPSO");
    public static NaifObject ATLAS = new(615, "ATLAS", "IAU_ATLAS");
    public static NaifObject PROMETHEUS = new(616, "PROMETHEUS", "IAU_PROMETHEUS");
    public static NaifObject PANDORA = new(617, "PANDORA", "IAU_PANDORA");
    public static NaifObject PAN = new(618, "PAN", "IAU_PAN");
    public static NaifObject YMIR = new(619, "YMIR", "IAU_YMIR");
    public static NaifObject PAALIAQ = new(620, "PAALIAQ", "IAU_PAALIAQ");
    public static NaifObject TARVOS = new(621, "TARVOS", "IAU_TARVOS");
    public static NaifObject IJIRAQ = new(622, "IJIRAQ", "IAU_IJIRAQ");
    public static NaifObject SUTTUNGR = new(623, "SUTTUNGR", "IAU_SUTTUNGR");
    public static NaifObject KIVIUQ = new(624, "KIVIUQ", "IAU_KIVIUQ");
    public static NaifObject MUNDILFARI = new(625, "MUNDILFARI", "IAU_MUNDILFARI");
    public static NaifObject ALBIORIX = new(626, "ALBIORIX", "IAU_ALBIORIX");
    public static NaifObject SKATHI = new(627, "SKATHI", "IAU_SKATHI");
    public static NaifObject ERRIAPUS = new(628, "ERRIAPUS", "IAU_ERRIAPUS");
    public static NaifObject SIARNAQ = new(629, "SIARNAQ", "IAU_SIARNAQ");
    public static NaifObject THRYMR = new(630, "THRYMR", "IAU_THRYMR");
    public static NaifObject NARVI = new(631, "NARVI", "IAU_NARVI");
    public static NaifObject METHONE = new(632, "METHONE", "IAU_METHONE");
    public static NaifObject PALLENE = new(633, "PALLENE", "IAU_PALLENE");
    public static NaifObject POLYDEUCES = new(634, "POLYDEUCES", "IAU_POLYDEUCES");
    public static NaifObject DAPHNIS = new(635, "DAPHNIS", "IAU_DAPHNIS");
    public static NaifObject AEGIR = new(636, "AEGIR", "IAU_AEGIR");
    public static NaifObject BEBHIONN = new(637, "BEBHIONN", "IAU_BEBHIONN");
    public static NaifObject BERGELMIR = new(638, "BERGELMIR", "IAU_BERGELMIR");
    public static NaifObject BESTLA = new(639, "BESTLA", "IAU_BESTLA");
    public static NaifObject FARBAUTI = new(640, "FARBAUTI", "IAU_FARBAUTI");
    public static NaifObject FENRIR = new(641, "FENRIR", "IAU_FENRIR");
    public static NaifObject FORNJOT = new(642, "FORNJOT", "IAU_FORNJOT");
    public static NaifObject HATI = new(643, "HATI", "IAU_HATI");
    public static NaifObject HYRROKKIN = new(644, "HYRROKKIN", "IAU_HYRROKKIN");
    public static NaifObject KARI = new(645, "KARI", "IAU_KARI");
    public static NaifObject LOGE = new(646, "LOGE", "IAU_LOGE");
    public static NaifObject SKOLL = new(647, "SKOLL", "IAU_SKOLL");
    public static NaifObject SURTUR = new(648, "SURTUR", "IAU_SURTUR");
    public static NaifObject ANTHE = new(649, "ANTHE", "IAU_ANTHE");
    public static NaifObject JARNSAXA = new(650, "JARNSAXA", "IAU_JARNSAXA");
    public static NaifObject GREIP = new(651, "GREIP", "IAU_GREIP");
    public static NaifObject TARQEQ = new(652, "TARQEQ", "IAU_TARQEQ");
    public static NaifObject AEGAEON = new(653, "AEGAEON", "IAU_AEGAEON");
    public static NaifObject URANUS = new(799, "URANUS", "IAU_URANUS");
    public static NaifObject ARIEL = new(701, "ARIEL", "IAU_ARIEL");
    public static NaifObject UMBRIEL = new(702, "UMBRIEL", "IAU_UMBRIEL");
    public static NaifObject TITANIA = new(703, "TITANIA", "IAU_TITANIA");
    public static NaifObject OBERON = new(704, "OBERON", "IAU_OBERON");
    public static NaifObject MIRANDA = new(705, "MIRANDA", "IAU_MIRANDA");
    public static NaifObject CORDELIA = new(706, "CORDELIA", "IAU_CORDELIA");
    public static NaifObject OPHELIA = new(707, "OPHELIA", "IAU_OPHELIA");
    public static NaifObject BIANCA = new(708, "BIANCA", "IAU_BIANCA");
    public static NaifObject CRESSIDA = new(709, "CRESSIDA", "IAU_CRESSIDA");
    public static NaifObject DESDEMONA = new(710, "DESDEMONA", "IAU_DESDEMONA");
    public static NaifObject JULIET = new(711, "JULIET", "IAU_JULIET");
    public static NaifObject PORTIA = new(712, "PORTIA", "IAU_PORTIA");
    public static NaifObject ROSALIND = new(713, "ROSALIND", "IAU_ROSALIND");
    public static NaifObject BELINDA = new(714, "BELINDA", "IAU_BELINDA");
    public static NaifObject PUCK = new(715, "PUCK", "IAU_PUCK");
    public static NaifObject CALIBAN = new(716, "CALIBAN", "IAU_CALIBAN");
    public static NaifObject SYCORAX = new(717, "SYCORAX", "IAU_SYCORAX");
    public static NaifObject PROSPERO = new(718, "PROSPERO", "IAU_PROSPERO");
    public static NaifObject SETEBOS = new(719, "SETEBOS", "IAU_SETEBOS");
    public static NaifObject STEPHANO = new(720, "STEPHANO", "IAU_STEPHANO");
    public static NaifObject TRINCULO = new(721, "TRINCULO", "IAU_TRINCULO");
    public static NaifObject FRANCISCO = new(722, "FRANCISCO", "IAU_FRANCISCO");
    public static NaifObject MARGARET = new(723, "MARGARET", "IAU_MARGARET");
    public static NaifObject FERDINAND = new(724, "FERDINAND", "IAU_FERDINAND");
    public static NaifObject PERDITA = new(725, "PERDITA", "IAU_PERDITA");
    public static NaifObject MAB = new(726, "MAB", "IAU_MAB");
    public static NaifObject CUPID = new(727, "CUPID", "IAU_CUPID");
    public static NaifObject NEPTUNE = new(899, "NEPTUNE", "IAU_NEPTUNE");
    public static NaifObject TRITON = new(801, "TRITON", "IAU_TRITON");
    public static NaifObject NEREID = new(802, "NEREID", "IAU_NEREID");
    public static NaifObject NAIAD = new(803, "NAIAD", "IAU_NAIAD");
    public static NaifObject THALASSA = new(804, "THALASSA", "IAU_THALASSA");
    public static NaifObject DESPINA = new(805, "DESPINA", "IAU_DESPINA");
    public static NaifObject GALATEA = new(806, "GALATEA", "IAU_GALATEA");
    public static NaifObject LARISSA = new(807, "LARISSA", "IAU_LARISSA");
    public static NaifObject PROTEUS = new(808, "PROTEUS", "IAU_PROTEUS");
    public static NaifObject HALIMEDE = new(809, "HALIMEDE", "IAU_HALIMEDE");
    public static NaifObject PSAMATHE = new(810, "PSAMATHE", "IAU_PSAMATHE");
    public static NaifObject SAO = new(811, "SAO", "IAU_SAO");
    public static NaifObject LAOMEDEIA = new(812, "LAOMEDEIA", "IAU_LAOMEDEIA");
    public static NaifObject NESO = new(813, "NESO", "IAU_NESO");
    public static NaifObject PLUTO = new(999, "PLUTO", "IAU_PLUTO");
    public static NaifObject CHARON = new(901, "CHARON", "IAU_CHARON");
    public static NaifObject NIX = new(902, "NIX", "IAU_NIX");
    public static NaifObject HYDRA = new(903, "HYDRA", "IAU_HYDRA");
    public static NaifObject KERBEROS = new(904, "KERBEROS", "IAU_KERBEROS");
    public static NaifObject STYX = new(905, "STYX", "IAU_STYX");

    public static CelestialBody EARTH_BODY;
    public static CelestialBody MOON_BODY;
    
    static PlanetsAndMoons()
    {
        EARTH_BODY = new CelestialBody(EARTH);
        MOON_BODY = new CelestialBody(MOON);
    }
}