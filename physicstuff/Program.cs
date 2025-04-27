using System;
using System.Threading;

class RocketSimulation
{
    // Atmospheric constants
    static double g0 = 9.80665;
    const double R = 287.05;
    const double gamma = 1.4;

    // ISA
    struct AtmoLayer { public double baseAlt, lapse, baseTemp, basePress; }
    static AtmoLayer[] isa = new[] {
        new AtmoLayer{ baseAlt=0,     lapse=-0.0065, baseTemp=288.15, basePress=101325 },
        new AtmoLayer{ baseAlt=11000, lapse=0.0,     baseTemp=216.65, basePress=22632.1 },
        new AtmoLayer{ baseAlt=20000, lapse=0.001,   baseTemp=216.65, basePress=5474.89 },
        new AtmoLayer{ baseAlt=32000, lapse=0.0028,  baseTemp=228.65, basePress=868.019 },
        new AtmoLayer{ baseAlt=47000, lapse=0.0,     baseTemp=270.65, basePress=110.906 },
        new AtmoLayer{ baseAlt=51000, lapse=-0.0028, baseTemp=270.65, basePress=66.9389 },
        new AtmoLayer{ baseAlt=71000, lapse=-0.002,  baseTemp=214.65, basePress=3.95642 }
    };

    static void Main(string[] args)
    {
        // Mission profile
        var stages = new[] {
            (dry:30000.0, fuel:400000.0, thrust:8e6, dia:5.0),
            (dry:8000.0,  fuel:150000.0, thrust:2e6, dia:4.0),
            (dry:3000.0,  fuel:50000.0,  thrust:0.5e6, dia:3.0)
        };
        int currentStage = 0;

        double timeStep = 0.1;
        double orbitAltitude = 400_000;
        double earthRadius = 6.371e6;
        double seaLevelP = 101325;

        // Crew and life support
        int crewCount = 4;
        double oxygenMass = 2000, co2Mass = 0;
        double oxygenRate = 0.5, co2Rate = 0.4; 
        double payloadMass = 1000;
        double altitude = 0, velocity = 0, mass = 0, gravity = 0, pitchAngle = 90;
        mass = SumStageMass(stages, currentStage) + payloadMass;

        Console.WriteLine("Starting simulation with {0} stages...", stages.Length);

        while (true)
        {
            // End simulation on final stage cutoff or orbit insertion
            if (currentStage >= stages.Length) break;
            if (altitude >= orbitAltitude) { Console.Clear(); Console.WriteLine("=> Orbit achieved!"); break; }

            // Compute gravity and atmospheric properties
            gravity = g0 * Math.Pow(earthRadius / (earthRadius + altitude), 2);
            var (density, pressure, temperature) = GetAtmosphere(altitude);
            double speedOfSound = Math.Sqrt(gamma * R * temperature);
            double mach = velocity / speedOfSound;

            var stage = stages[currentStage];
            if (stage.fuel <= 0)
            {
                Console.Clear(); Console.WriteLine($"=> Stage {currentStage + 1} jettisoned!");
                mass -= stage.dry;
                currentStage++;
                Thread.Sleep(2000); continue;
            }

            // Aerodynamic drag coefficient vs Mach number
            double cd = mach < 0.8 ? 0.5 : (mach < 1.2 ? 0.5 + (mach - 0.8) / 0.4 * 0.3 : 0.8);
            double area = Math.PI * stage.dia * stage.dia / 4;
            double dynamicPressure = 0.5 * density * velocity * velocity;

            // Throttle to limit Max-Q above 5 km
            double throttle = 1.0;
            if (altitude > 5000)
            {
                throttle = Math.Clamp(dynamicPressure > 35000 ? 35000 / dynamicPressure : 1.0, 0, 1);
            }

            // Constant sea-level value, linear to vacuum
            double ispSea = 300;
            double ispVac = 450;
            double isp = ispSea + (ispVac - ispSea) * (1 - pressure / seaLevelP);
            isp = Math.Clamp(isp, ispSea, ispVac);
            double exhaustVelocity = isp * g0;

            // Thrust and fuel mass flow
            double thrust = stage.thrust * throttle;
            double massFlow = thrust / exhaustVelocity;
            double fuelUsed = massFlow * timeStep;

            // Forces
            double drag = 0.5 * density * velocity * velocity * cd * area;
            double netForce = thrust - drag - mass * gravity;
            double acceleration = netForce / mass;

            // pitch reduction 90°→0° between 10–100 km
            if (altitude > 10000 && altitude < 100000)
                pitchAngle = 90 - (altitude - 10000) / (100000 - 10000) * 90;
            else if (altitude >= 100000)
                pitchAngle = 0;

            // States
            velocity += acceleration * timeStep;
            altitude += velocity * timeStep;
            stages[currentStage].fuel -= fuelUsed;
            mass -= fuelUsed;
            oxygenMass -= oxygenRate * crewCount * timeStep / 3600;
            co2Mass += co2Rate * crewCount * timeStep / 3600;

            // Output
            Console.Clear();
            DrawStageIndicator(currentStage + 1, stages.Length);
            DrawTelemetry(altitude, velocity, acceleration, thrust, netForce, massFlow,
                          mass, density, drag, dynamicPressure, gravity,
                          isp, 101325, oxygenMass, co2Mass, mach, cd, pitchAngle);
            DrawRocket(altitude, velocity);
            DrawAtmosphereLayer(altitude);

            Thread.Sleep((int)(timeStep * 1000));
        }
        Console.WriteLine("Simulation finished.");
    }

    static double SumStageMass((double dry, double fuel, double thrust, double dia)[] s, int startIndex)
    {
        double m = 0;
        for (int i = startIndex; i < s.Length; i++) m += s[i].dry + s[i].fuel;
        return m;
    }

    static (double density, double press, double temp) GetAtmosphere(double alt)
    {
        foreach (var L in isa.Reverse())
        {
            if (alt >= L.baseAlt)
            {
                double dH = alt - L.baseAlt;
                double T = L.baseTemp + L.lapse * dH;
                double P = L.lapse == 0
                    ? L.basePress * Math.Exp(-g0 * dH / (R * L.baseTemp))
                    : L.basePress * Math.Pow(L.baseTemp / (L.baseTemp + L.lapse * dH), g0 / (L.lapse * R));
                double rho = P / (R * T);
                return (rho, P, T);
            }
        }
        return (0, 0, 0);
    }

    static void DrawStageIndicator(int current, int total)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write("Stage: ");
        for (int i = 1; i <= total; i++) Console.Write(i == current ? "[#]" : "[ ]");
        Console.WriteLine();
        Console.ResetColor();
    }

    static void DrawTelemetry(double alt, double vel, double accel, double thrust, double netF,
        double mflow, double mass, double rho, double drag, double q, double g,
        double isp, double cabinP, double oxy, double co2, double mach, double cd, double pitch)
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("╔═ Telemetry ═════════════════════════════════╗");
        Console.WriteLine($"│ Altitude       : {alt:F0} m    Mach: {mach:F2}  ");
        Console.WriteLine($"│ Velocity       : {vel:F1} m/s      Cd: {cd:F2}  ");
        Console.WriteLine($"│ Acceleration   : {accel:F2} m/s²                ");
        Console.WriteLine($"│ Total Thrust   : {thrust:F0} N                  ");
        Console.WriteLine($"│ Net Thrust     : {netF:F0} N                    ");
        Console.WriteLine($"│ Mass Flow      : {mflow:F1} kg/s                ");
        Console.WriteLine($"│ Total Mass     : {mass:F0} kg                   ");
        Console.WriteLine($"│ Air Density    : {rho:F4} kg/m³                 ");
        Console.WriteLine($"│ Drag Force     : {drag:F0} N                    ");
        Console.WriteLine($"│ Dynamic Pressure: {q:F0} Pa                     ");
        Console.WriteLine($"│ Isp            : {isp:F1} s                     ");
        Console.WriteLine($"│ Cabin Pressure : {cabinP:F0} Pa                 ");
        Console.WriteLine($"│ O₂ Remaining   : {oxy:F1} kg                    ");
        Console.WriteLine($"│ CO₂ Produced   : {co2:F1} kg                    ");
        Console.WriteLine($"│ Pitch Angle    : {pitch:F2}°                    ");
        Console.WriteLine("╚══════════════════════════════════════════════╝");
        Console.ResetColor();
    }

    static void DrawRocket(double alt, double vel)
    {
        int bars = (int)(alt / 5000);
        string rocket = new string('#', bars);
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Ascent Progress: [{rocket.PadRight(60)}] {alt:F0} m");
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Speed          : {vel:F0} m/s");
        Console.ResetColor();
    }

    static void DrawAtmosphereLayer(double alt)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        if (alt < 11000) Console.WriteLine("Troposphere");
        else if (alt < 20000) Console.WriteLine("Stratosphere");
        else if (alt < 32000) Console.WriteLine("Stratopause");
        else if (alt < 47000) Console.WriteLine("Mesosphere");
        else Console.WriteLine("Thermosphere");
        Console.ResetColor();
    }
}
