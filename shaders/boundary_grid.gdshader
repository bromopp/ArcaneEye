shader_type spatial;
render_mode cull_front, depth_draw_always, blend_mix, unshaded;

uniform float world_size : hint_range(10.0, 200.0) = 100.0;
uniform float proximity_distance : hint_range(5.0, 50.0) = 20.0;
uniform float distortion_strength : hint_range(0.0, 2.0) = 0.5;
uniform float wormhole_speed : hint_range(0.5, 5.0) = 2.0;
uniform float spiral_intensity : hint_range(0.0, 3.0) = 1.5;
uniform vec3 player_position;

varying vec3 world_position;

vec3 create_wormhole_effect(vec2 coord, vec2 player_center, float time, float intensity) {
    // Calculate UV relative to player center
    vec2 uv = coord - player_center;
    
    // Rotate the entire coordinate system
    float rotation_angle = time * wormhole_speed;
    float cos_rot = cos(rotation_angle);
    float sin_rot = sin(rotation_angle);
    vec2 rotated_uv = vec2(
        uv.x * cos_rot - uv.y * sin_rot,
        uv.x * sin_rot + uv.y * cos_rot
    );
    
    // Calculate distance from center (same for both original and rotated)
    float len = length(uv);
    
    // Create proximity-based black center radius
    // Larger black center when intensity (proximity) is higher
    float base_black_radius = 2.5; // Minimum black center radius
    float max_black_radius = 4.5;  // Maximum black center radius
    float black_radius = mix(base_black_radius, max_black_radius, intensity);
    
    // Use rotated coordinates for angle calculation
    float angle = atan(rotated_uv.y, rotated_uv.x);
    
    // Create event horizon - black center with transition based on proximity
    float event_horizon = smoothstep(black_radius * 0.8, black_radius + 1.5, len);
    
    // Create separate mask for true black center (100% opaque) - larger and proximity-based
    float black_center = 1.0 - smoothstep(black_radius * 0.5, black_radius, len);
    
    // Create outer white ring around the black center
    float white_ring_inner = black_radius;
    float white_ring_outer = black_radius + 0.5;
    float white_ring = smoothstep(white_ring_inner, white_ring_inner + 0.1, len) * 
                      (1.0 - smoothstep(white_ring_outer - 0.1, white_ring_outer, len));
    
    // Create swirling energy patterns based on rotated coordinates - constant thickness
    float swirl_pattern = sin(angle * 3.0 + len * spiral_intensity) * 0.5 + 0.5;
    float radial_pattern = sin(len * 4.0) * 0.3 + 0.7;
    
    // Create turbulence for more realistic plasma motion - constant
    float turbulence = sin(angle * 8.0 + len * 6.0) * 0.2 + 0.8;
    
    // Energy intensity falls off with distance
    float energy_falloff = 1.0 / (1.0 + len * 0.2);
    
    // Combine patterns for temperature simulation
    float temperature = event_horizon * energy_falloff * swirl_pattern * radial_pattern * turbulence;
    
    // Realistic accretion disk colors with enhanced contrast
    vec3 disk_color;
    if (temperature < 0.2) {
        // Hottest regions - bright white
        disk_color = vec3(1.0, 1.0, 1.0);
    } else if (temperature < 0.5) {
        // Hot regions - white to orange
        float t = (temperature - 0.2) / 0.3;
        disk_color = mix(vec3(1.0, 1.0, 1.0), vec3(1.0, 0.7, 0.3), t);
    } else if (temperature < 0.8) {
        // Medium regions - orange
        float t = (temperature - 0.5) / 0.3;
        disk_color = mix(vec3(1.0, 0.7, 0.3), vec3(1.0, 0.4, 0.1), t);
    } else {
        // Cooler regions - dark orange to red
        float t = (temperature - 0.8) / 0.2;
        disk_color = mix(vec3(1.0, 0.4, 0.1), vec3(0.6, 0.2, 0.05), t);
    }
    
    // Apply swirl intensity modulation
    float swirl_modulation = swirl_pattern * turbulence;
    
    // Combine everything
    vec3 final_color = disk_color * event_horizon * swirl_modulation * intensity;
    
    // Keep brightness constant - no variations
    // float brightness_variation = sin(wormhole_speed * 0.7 + len) * 0.1 + 0.9;
    // final_color *= brightness_variation;
    
    // Add white ring effect
    vec3 white_ring_color = vec3(1.0, 1.0, 1.0) * white_ring;
    final_color += white_ring_color;
    
    // Force black center to be completely black (after adding white ring)
    final_color = mix(final_color, vec3(0.0, 0.0, 0.0), black_center);
    
    return final_color;
}

float get_boundary_distance(vec3 pos, float world_half) {
    return min(
        min(world_half - abs(pos.x), world_half - abs(pos.y)),
        world_half - abs(pos.z)
    );
}

void vertex() {
    world_position = VERTEX;
}

void fragment() {
    float world_half = world_size * 0.5;
    float boundary_dist = get_boundary_distance(player_position, world_half);
    
    // Only show grid when player is near boundary
    if (boundary_dist > proximity_distance) {
        discard;
    }
    
    // Determine which boundary face we're on and calculate face-specific proximity
    vec3 abs_pos = abs(world_position);
    bool on_boundary = false;
    vec2 grid_coord;
    vec2 player_center;
    float face_proximity = 0.0;
    
    // X boundary faces
    if (abs(abs_pos.x - world_half) < 0.1) {
        grid_coord = world_position.yz;
        // Project player position onto this YZ face
        player_center = player_position.yz;
        if (abs(player_position.x) > world_half - proximity_distance) {
            on_boundary = true;
            // Calculate distance from player to this specific X boundary face
            float player_to_face = world_half - abs(player_position.x);
            face_proximity = 1.0 - (player_to_face / proximity_distance);
        }
    }
    // Y boundary faces  
    else if (abs(abs_pos.y - world_half) < 0.1) {
        grid_coord = world_position.xz;
        // Project player position onto this XZ face
        player_center = player_position.xz;
        if (abs(player_position.y) > world_half - proximity_distance) {
            on_boundary = true;
            // Calculate distance from player to this specific Y boundary face
            float player_to_face = world_half - abs(player_position.y);
            face_proximity = 1.0 - (player_to_face / proximity_distance);
        }
    }
    // Z boundary faces
    else if (abs(abs_pos.z - world_half) < 0.1) {
        grid_coord = world_position.xy;
        // Project player position onto this XY face
        player_center = player_position.xy;
        if (abs(player_position.z) > world_half - proximity_distance) {
            on_boundary = true;
            // Calculate distance from player to this specific Z boundary face
            float player_to_face = world_half - abs(player_position.z);
            face_proximity = 1.0 - (player_to_face / proximity_distance);
        }
    }
    
    if (!on_boundary) {
        discard;
    }
    
    // Smooth proximity fade using smoothstep for better visual transition
    float proximity_fade = smoothstep(0.0, 1.0, face_proximity);
    
    // Add additional distance-based fade for fragments far from player
    vec3 fragment_to_player = player_position - world_position;
    float fragment_distance = length(fragment_to_player);
    float max_visible_distance = proximity_distance * 1.2; // Tighter visibility range
    float distance_fade = 1.0 - smoothstep(proximity_distance * 0.3, max_visible_distance, fragment_distance);
    
    // Combine proximity and distance fades
    float combined_fade = proximity_fade * distance_fade;
    
    // Discard fragments that are not significantly affected by proximity
    if (combined_fade < 0.05) {
        discard;
    }
    
    // Create wormhole distortion effect centered on player
    vec3 wormhole_color = create_wormhole_effect(grid_coord, player_center, TIME, combined_fade * distortion_strength);
    
    // Calculate final intensity
    float wormhole_intensity = length(wormhole_color) * combined_fade;
    
    // Calculate distance from player center for effects
    vec2 pos = grid_coord - player_center;
    float dist_from_center = length(pos);
    
    // Create proximity-based black center mask (same calculation as in wormhole effect)
    float base_black_radius = 2.5;
    float max_black_radius = 4.5;
    float black_radius = mix(base_black_radius, max_black_radius, combined_fade);
    float black_center = 1.0 - smoothstep(black_radius * 0.5, black_radius, dist_from_center);
    
    // Create white ring mask (same as in wormhole effect)
    float white_ring_inner = black_radius;
    float white_ring_outer = black_radius + 0.5;
    float white_ring = smoothstep(white_ring_inner, white_ring_inner + 0.1, dist_from_center) * 
                      (1.0 - smoothstep(white_ring_outer - 0.1, white_ring_outer, dist_from_center));
    
    // Discard fragments that are too transparent, but keep black center and white ring
    if (wormhole_intensity < 0.02 && black_center < 0.5 && white_ring < 0.1) {
        discard;
    }
    
    // Add subtle energy fluctuations for realism (constant, not time-based)
    float energy_fluctuation = 0.85; // Constant energy level
    
    // Create outer glow effect
    float glow_radius = 8.0; // Adjust glow size
    float outer_glow = 1.0 - smoothstep(3.0, glow_radius, dist_from_center);
    outer_glow = pow(outer_glow, 2.0); // Make glow falloff more dramatic
    
    // Enhance colors with glow
    vec3 final_color = wormhole_color * energy_fluctuation;
    
    // Add orange glow around the edges
    vec3 glow_color = vec3(1.0, 0.4, 0.1) * outer_glow * 0.6;
    final_color += glow_color;
    
    // Force black center to be completely black (override any orange coloring)
    final_color = mix(final_color, vec3(0.0, 0.0, 0.0), black_center);
    
    // Calculate alpha - black center and white ring are opaque, others follow intensity
    float final_alpha;
    if (black_center > 0.1) {
        final_alpha = 1.0; // 100% opaque for black center
    } else if (white_ring > 0.1) {
        final_alpha = white_ring; // White ring opacity
    } else {
        final_alpha = max(wormhole_intensity, outer_glow * 0.3) * 0.9;
    }
    
    ALBEDO = final_color;
    ALPHA = final_alpha;
    
    // Strong emission for sci-fi glow effect (but not in black center)
    EMISSION = final_color * (1.0 - black_center) * 0.7 + glow_color * (1.0 - black_center) * 0.5;
}