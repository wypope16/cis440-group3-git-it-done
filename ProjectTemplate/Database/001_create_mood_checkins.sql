CREATE TABLE IF NOT EXISTS mood_checkins (
    checkin_id INT NOT NULL AUTO_INCREMENT,
    mood VARCHAR(20) NOT NULL,
    workplace_factor VARCHAR(50) NOT NULL,
    cause_text TEXT NOT NULL,
    recommendation_text TEXT NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (checkin_id)
) ENGINE=InnoDB
  DEFAULT CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;