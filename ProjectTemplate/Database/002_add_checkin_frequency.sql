-- ============================================================
-- US-07: CHECK-IN FREQUENCY DATABASE SETUP
-- ============================================================

-- Stores the company-wide number of days required
-- between accepted anonymous employee check-ins.
CREATE TABLE IF NOT EXISTS checkin_settings (
    setting_id TINYINT NOT NULL,
    frequency_days INT NOT NULL DEFAULT 7,
    updated_at TIMESTAMP NOT NULL
        DEFAULT CURRENT_TIMESTAMP
        ON UPDATE CURRENT_TIMESTAMP,
    PRIMARY KEY (setting_id)
) ENGINE=InnoDB
DEFAULT CHARACTER SET utf8mb4
COLLATE utf8mb4_unicode_ci;


-- The application uses one company-wide frequency setting.
-- INSERT IGNORE keeps this script safe if the row already exists.
INSERT IGNORE INTO checkin_settings (
    setting_id,
    frequency_days
)
VALUES (
    1,
    7
);


-- ============================================================
-- US-07: ANONYMOUS FREQUENCY-CONTROL LOG
-- ============================================================
--
-- anonymous_token stores ONLY the SHA-256 hash of the random
-- browser-control token.
--
-- It does not contain:
-- employee name
-- employee ID
-- email address
-- mood
-- cause/explanation
-- recommendation
--
-- Frequency-control information remains separate from
-- the anonymous feedback stored in mood_checkins.
-- ============================================================

CREATE TABLE IF NOT EXISTS checkin_frequency_log (
    anonymous_token VARCHAR(128) NOT NULL,
    last_submitted_at DATETIME NOT NULL,
    PRIMARY KEY (anonymous_token)
) ENGINE=InnoDB
DEFAULT CHARACTER SET utf8mb4
COLLATE utf8mb4_unicode_ci;