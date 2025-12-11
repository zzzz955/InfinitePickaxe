# auth_schema 설계 (MVP)

## users
| column        | type         | constraint/default        | note                                      |
| ---           | ---          | ---                       | ---                                       |
| user_id       | UUID         | PK, `gen_random_uuid()`   |                                           |
| provider      | VARCHAR(30)  | NOT NULL, DEFAULT 'google'| 소셜 제공자 (google/apple/…)              |
| external_id   | VARCHAR(255) | UNIQUE, NOT NULL          | `{provider}-{provider_user_id}`           |
| nickname      | VARCHAR(50)  |                           | 선택, 중복 가능, 검증 규칙 적용           |
| email         | VARCHAR(255) |                           | 선택                                      |
| created_at    | TIMESTAMP    | DEFAULT now               |                                           |
| updated_at    | TIMESTAMP    | DEFAULT now               |                                           |
| last_login    | TIMESTAMP    |                           |                                           |
| last_logout   | TIMESTAMP    |                           |                                           |
| is_banned     | BOOLEAN      | DEFAULT false             |                                           |
| ban_reason    | TEXT         |                           |                                           |
| banned_at     | TIMESTAMP    |                           |                                           |
| banned_until  | TIMESTAMP    |                           | NULL => 영구                              |

닉네임 검증 규칙: 한글/숫자 2~8자 또는 영문/숫자 4~16자, 특수문자 불가.

## jwt_families
| column            | type      | constraint/default              | note                           |
| ---               | ---       | ---                             | ---                            |
| family_id         | UUID      | PK                              | 리프레시 패밀리                |
| user_id           | UUID      | FK→users                        |                                |
| device_id         | VARCHAR   |                                 |                                |
| login_ip          | INET      |                                 |                                |
| user_agent        | TEXT      |                                 |                                |
| is_active         | BOOLEAN   | DEFAULT true                    |                                |
| is_revoked        | BOOLEAN   | DEFAULT false                   |                                |
| revoked_reason    | TEXT      |                                 |                                |
| created_at        | TIMESTAMP | DEFAULT now                     |                                |
| last_refreshed_at | TIMESTAMP | DEFAULT now                     |                                |
| expires_at        | TIMESTAMP | NOT NULL                        |                                |
| revoked_at        | TIMESTAMP |                                 |                                |
| refresh_count     | INTEGER   | DEFAULT 0                       |                                |
| max_refresh_count | INTEGER   | DEFAULT 100, CHECK              |                                |

## jwt_tokens
| column     | type      | constraint/default              | note          |
| ---        | ---       | ---                             | ---           |
| token_id   | UUID      | PK                              |               |
| family_id  | UUID      | FK→jwt_families                 |               |
| user_id    | UUID      | FK→users                        |               |
| token_hash | VARCHAR   | UNIQUE, NOT NULL                | SHA-256       |
| jti        | VARCHAR   | UNIQUE, NOT NULL                | JWT ID        |
| is_valid   | BOOLEAN   | DEFAULT true                    |               |
| is_used    | BOOLEAN   | DEFAULT false                   |               |
| issued_at  | TIMESTAMP | DEFAULT now                     |               |
| expires_at | TIMESTAMP | NOT NULL                        |               |
| used_at    | TIMESTAMP |                                 |               |
| revoked_at | TIMESTAMP |                                 |               |
| issued_ip  | INET      |                                 |               |
| used_ip    | INET      |                                 |               |

## session_history
| column            | type      | constraint/default      | note                              |
| ---               | ---       | ---                     | ---                               |
| session_id        | UUID      | PK                      |                                   |
| user_id           | UUID      | FK→users                |                                   |
| provider          | VARCHAR   |                         |                                   |
| external_id       | VARCHAR   |                         |                                   |
| device_id         | VARCHAR   |                         |                                   |
| client_ip         | INET      |                         |                                   |
| user_agent        | TEXT      |                         |                                   |
| result            | VARCHAR   |                         | SUCCESS / FAIL / BANNED / INVALID |
| reason            | TEXT      |                         | 에러/거부 사유                     |
| login_at          | TIMESTAMP | DEFAULT now             |                                   |
| logout_at         | TIMESTAMP |                         |                                   |
| duration_seconds  | INTEGER   |                         |                                   |
| created_at        | TIMESTAMP | DEFAULT now             |                                   |

## ban_history
| column      | type      | constraint/default       | note                     |
| ---         | ---       | ---                      | ---                      |
| ban_id      | UUID      | PK                       |                          |
| user_id     | UUID      | FK→users                 |                          |
| ban_reason  | TEXT      | NOT NULL                 |                          |
| ban_type    | VARCHAR   | NOT NULL                 | TEMPORARY/PERMANENT      |
| banned_by   | VARCHAR   | DEFAULT 'SYSTEM'         |                          |
| banned_at   | TIMESTAMP | DEFAULT now              |                          |
| banned_until| TIMESTAMP |                          | NULL => 영구             |
| unbanned_at | TIMESTAMP |                          |                          |
| unban_reason| TEXT      |                          |                          |
