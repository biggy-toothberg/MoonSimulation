# MoonSimulation
This simulation integrates realistic physics—gravity variation, standard atmosphere, aerodynamic drag, thrust modulation, and a simple guidance law—to produce an end-to-end multi-stage launch profile.


- **Staged Rocket Architecture**  
  - Three discrete stages, each with its own dry mass, fuel mass, thrust and diameter  
  - Mass calculation sums dry + fuel of current and all upper stages plus payload  

- **Gravity Model**  
  - Uses inverse-square law:  
    \[  
      g(h) = g_0 \Bigl(\frac{R_\text{earth}}{R_\text{earth}+h}\Bigr)^2  
    \]  
  - Accounts for decreasing gravity with altitude  

- **Standard Atmosphere (ISA)**  
  - Piecewise layers (troposphere, stratosphere, etc.) with linear lapse rates  
  - Calculates local temperature \(T\) and pressure \(P\), then density \(\rho = P/(R\,T)\)  

- **Mach-Dependent Drag Coefficient**  
  - \(C_d = 0.5\) for \(M<0.8\), rises linearly up to 0.8 for \(0.8<M<1.2\), then constant  

- **Aerodynamic Drag & Dynamic Pressure**  
  - Drag force \(D = \tfrac12\,\rho\,v^2\,C_d\,A\)  
  - Dynamic pressure \(q = \tfrac12\,\rho\,v^2\) used to throttle (Max-Q limit above 5 km)  

- **Throttle-to-Max-Q**  
  - Below 5 km: full throttle  
  - Above 5 km: reduces thrust so that \(q \le 35{,}000\) Pa  

- **Specific Impulse Model**  
  - Constant sea-level \(I_{sp}=300\) s  
  - Linear interpolation toward vacuum \(I_{sp}=450\) s as pressure drops  

- **Fuel Consumption**  
  - Mass flow \(\dot m = F / v_e\)  
  - Fuel removed each timestep \(\Delta m = \dot m \times \Delta t\)  

- **Gravity Turn Guidance**  
  - Simple linear pitch from 90° (vertical) at 10 km to 0° (horizontal) at 100 km  

- **Payload & Life Support**  
  - Includes payload mass in total inertia  
  - Tracks onboard O₂ consumption and CO₂ production per crew-hour  

- **Numerical Integration**  
  - Euler forward integration of velocity and altitude every 0.1 s  

- **Visual Console Outputs**  
  - Stage indicator  
  - Telemetry box with altitude, velocity, acceleration, thrust, mass flow, drag, density, dynamic pressure, Isp, cabin pressure, O₂/CO₂, Mach number, \(C_d\), pitch angle  
  - ASCII-style “Ascent Progress” and atmospheric layer name  

- **Behavioural Highlights**  
  - Stages automatically jettison when fuel depletes, with a 2 s coasting pause  
  - Thrust and drag forces balance against weight to compute net acceleration  
  - Simulation ends when all stages expended or target orbital altitude reached  
